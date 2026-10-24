using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Application.Helpers;
using Domain.NovelModels;
using Domain.Websites;
using HtmlAgilityPack;

namespace Application.Implementation.NovelWebsites;

// Scraper for https://www.hetushu.com (和图书, a Chinese web-novel site).
//
// NETWORK NOTE:
// The site is behind Cloudflare (403 for plain clients), so every request goes through
// FlareSolverrUtilityFunctions: FlareSolverr solves once, then plain HTTP with its cookies + User-Agent,
// re-solving automatically when the clearance expires mid-run.
//
// ANTI-SCRAPING NOTE (reverse-engineered from /command/section.js):
//  * The chapter HTML contains every paragraph, but in SHUFFLED order.
//  * The correct order comes from /book/<id>/r<chapterId>.json, which answers 204 with a `token` header
//    (or 200 with the token as body). base64(token) split on /[A-Z]+%/ gives one number per paragraph.
//  * In the browser the script then appends hidden decoy copies of random paragraphs. That's why we must
//    parse the raw server HTML (plainOnly) and never FlareSolverr's rendered page.
//  * Paragraphs contain watermark elements ("和图书", the site's domain...) that section.css hides with
//    display:none; we drop them with their text.
public class Hetushu : NovelWebsite
{
    private const string BaseUrl = "https://www.hetushu.com";

    // Tags hidden inside #content by section.css (the watermarks).
    private static readonly HashSet<string> HiddenTags = new()
    {
        "acronym", "bdo", "big", "cite", "code", "dfn", "kbd", "q", "s", "samp", "strike", "tt", "u", "var"
    };

    // Optional cap on chapters fetched at once. null = no cap: concurrency is whatever BatchProcessor's
    // batch size allows (like KolNovel/Riwyat). Set a number if the site starts rate-limiting.
    private static readonly int? MaxConcurrentChapters = null;
    private const int MaxChapterAttempts = 3;
    private static readonly SemaphoreSlim ChapterGate =
        MaxConcurrentChapters is { } max ? new SemaphoreSlim(max) : null;

    public override async Task<IList<ChapterLinkInfo>> GetAllPages(string url) =>
        (await GetVolumePages(url)).SelectMany(v => v.Chapters).ToList();

    // <dl id="dir"><dt>volume name</dt><dd><a href="/book/<id>/<chapterId>.html">title</a></dd>...<dt>...</dl>
    // Each <dt> starts a volume holding the <dd> links after it; headings with no chapters are dropped.
    public override async Task<IList<VolumeLinkInfo>> GetVolumePages(string url)
    {
        var doc = await GetDocument(ToCatalogUrl(url));

        var dir = doc.GetElementbyId("dir");
        if (dir == null)
            return new List<VolumeLinkInfo>();

        var volumes = new List<VolumeLinkInfo>();
        foreach (var node in dir.ChildNodes)
        {
            if (node.Name == "dt")
            {
                volumes.Add(new VolumeLinkInfo
                {
                    Title = HttpUtility.HtmlDecode(node.InnerText).Trim(),
                    Chapters = new List<ChapterLinkInfo>()
                });
                continue;
            }

            var link = node.Name == "dd" ? node.Descendants("a").FirstOrDefault() : null;
            if (string.IsNullOrWhiteSpace(link?.GetAttributeValue("href", "")))
                continue;

            // Chapters before any <dt> still need a volume.
            if (volumes.Count == 0)
                volumes.Add(new VolumeLinkInfo { Title = "Volume 1", Chapters = new List<ChapterLinkInfo>() });

            volumes[^1].Chapters.Add(new ChapterLinkInfo
            {
                Info = HttpUtility.HtmlDecode(link.InnerText).Trim(),
                Url = MakeAbsolute(link.GetAttributeValue("href", ""))
            });
        }

        return volumes.Where(v => v.Chapters.Count > 0).ToList();
    }

    public override async Task<string> GetNovelName(string url)
    {
        var doc = await GetDocument(ToCatalogUrl(url));
        return HttpUtility.HtmlDecode(doc.DocumentNode.Descendants("h2").FirstOrDefault()?.InnerText ?? "").Trim();
    }

    public override async Task<Chapter> GetChapter(string url)
    {
        if (ChapterGate != null)
            await ChapterGate.WaitAsync();
        try
        {
            // A page/token mismatch (e.g. the CDN serving a page and a token from different versions) is
            // retried with a fresh fetch of both.
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await FetchChapter(url);
                }
                catch (InvalidDataException e) when (attempt < MaxChapterAttempts)
                {
                    Console.WriteLine($"Hetushu chapter retry {attempt}/{MaxChapterAttempts} >> {url}, {e.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                }
            }
        }
        finally
        {
            ChapterGate?.Release();
        }
    }

    private static async Task<Chapter> FetchChapter(string url)
    {
        var doc = await GetDocument(url);
        var order = await GetParagraphOrder(url);

        var content = doc.GetElementbyId("content")
                      ?? throw new InvalidDataException("No #content in chapter page");

        // <h2>volume</h2><h2 class="h2">chapter</h2>: the volume is already known from the catalog, so the
        // title is just the chapter heading (the last <h2>).
        var title = HttpUtility.HtmlDecode(content.Elements("h2").LastOrDefault()?.InnerText ?? "").Trim();

        var paragraphs = GetShuffledParagraphs(content);
        if (paragraphs.Count != order.Count)
            throw new InvalidDataException($"{paragraphs.Count} paragraphs but token has {order.Count} positions");

        // Same placement rule as section.js: positions below 5 are used as-is (and counted in b), the
        // others are shifted down by how many of those came before.
        var slots = new SortedDictionary<int, HtmlNode>();
        var b = 0;
        for (var i = 0; i < order.Count; i++)
        {
            if (order[i] < 5)
            {
                slots[order[i]] = paragraphs[i];
                b++;
            }
            else
            {
                slots[order[i] - b] = paragraphs[i];
            }
        }

        return new Chapter
        {
            Title = title,
            Body = slots.Values
                .Select(CleanParagraph)
                .Where(s => string.IsNullOrWhiteSpace(s) == false)
                .ToList()
        };
    }

    // section.js: drop #content's first child (the mask div); the paragraphs start right after the last
    // <h2> that precedes the first <div> without class "chapter".
    private static List<HtmlNode> GetShuffledParagraphs(HtmlNode content)
    {
        var nodes = content.ChildNodes.Skip(1).ToList();

        var start = 0;
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Name == "h2")
                start = i + 1;
            if (nodes[i].Name == "div" && nodes[i].GetAttributeValue("class", "") != "chapter")
                break;
        }

        return nodes.Skip(start).ToList();
    }

    // Fetches r<chapterId>.json and decodes its token into paragraph positions.
    private static async Task<List<int>> GetParagraphOrder(string chapterUrl)
    {
        var tokenUrl = Regex.Replace(chapterUrl, @"/(\d+)\.html$", "/r$1.json");
        if (tokenUrl == chapterUrl)
            throw new ArgumentException($"Not a chapter url: {chapterUrl}");

        // The origin only answers like section.js's XHR is answered when these headers are present; otherwise it
        // 404s -- and Cloudflare caches that 404 for everyone (cf-cache-status: HIT) for hours.
        var headers = new Dictionary<string, string>
        {
            ["Referer"] = chapterUrl,
            ["X-Requested-With"] = "XMLHttpRequest"
        };

        FlareSolverrUtilityFunctions.PlainResponse response;
        try
        {
            response = await FlareSolverrUtilityFunctions.GetPlainResponse(tokenUrl, chapterUrl, headers);
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            // A poisoned (cached) 404: a query string makes it a new cache key, so the request reaches the origin.
            response = await FlareSolverrUtilityFunctions.GetPlainResponse(
                $"{tokenUrl}?_={DateTime.UtcNow.Ticks}", chapterUrl, headers);
        }

        var token = response.StatusCode == HttpStatusCode.NoContent
            ? response.Headers.GetValueOrDefault("token")
            : response.Body;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidDataException($"Empty paragraph-order token ({tokenUrl})");

        token = token.Trim();
        var decoded = Encoding.ASCII.GetString(Convert.FromBase64String(token.PadRight(token.Length + (4 - token.Length % 4) % 4, '=')));

        return Regex.Split(decoded, "[A-Z]+%")
            .Where(s => s.Length > 0)
            .Select(int.Parse)
            .ToList();
    }

    // Removes the hidden watermark elements (with their text), then returns the visible text.
    private static string CleanParagraph(HtmlNode paragraph)
    {
        var clone = paragraph.CloneNode(true);
        foreach (var hidden in clone.Descendants().Where(n => HiddenTags.Contains(n.Name)).ToList())
            hidden.Remove();

        return HttpUtility.HtmlDecode(clone.InnerText).Trim();
    }

    // Always the raw server HTML (see ANTI-SCRAPING NOTE), never FlareSolverr's rendered DOM.
    private static async Task<HtmlDocument> GetDocument(string url)
    {
        Console.WriteLine($"Getting {url}");
        var html = await FlareSolverrUtilityFunctions.GetHtmlFromUrl(url, plainOnly: true);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    // Any book url (…/book/<id>/, …/book/<id>/index.html, a chapter url) -> the catalog …/book/<id>/index.html.
    private static string ToCatalogUrl(string url)
    {
        var m = Regex.Match(url, @"/book/(\d+)");
        return m.Success ? $"{BaseUrl}/book/{m.Groups[1].Value}/index.html" : url;
    }

    private static string MakeAbsolute(string href)
    {
        if (href.StartsWith("http://") || href.StartsWith("https://"))
            return href;

        return href.StartsWith("/") ? BaseUrl + href : BaseUrl + "/" + href;
    }
}
