using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Domain.NovelModels;
using Domain.Websites;
using HtmlAgilityPack;

namespace Application.Implementation.NovelWebsites;

// Scraper for https://www.69shuba.com (a Chinese web-novel site).
//
// NETWORK NOTE:
// 69shuba is behind Cloudflare, which blocks .NET's HttpClient by its TLS/JA3
// fingerprint -- a correct browser User-Agent is NOT enough (the request still 403s).
// The system `curl` passes that check, so we shell out to it. Two more quirks:
//  * only the bare domain works (www.69shuba.com 403s), and
//  * chapter pages 403 unless a Referer is sent (the site root works for every page).
//
// ENCODING NOTE:
// 69shuba serves GBK/GB2312 but its Content-Type header lies (the book index sends no
// charset; a chapter sends "charset=UTF-8" while the bytes are GBK). So we read the raw
// bytes from curl and decode them with the GBK code page. GBK (code page 936) is not
// registered by default on modern .NET, so the static constructor registers the
// code-pages provider once.
public class Shuba69 : NovelWebsite
{
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    // Chapter pages 403 without a Referer; the site root works for every page.
    private const string Referer = "https://69shuba.com/";

    static Shuba69()
    {
        // Enable legacy code pages (GBK/GB2312 = code page 936) on .NET Core+.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public override async Task<IList<ChapterLinkInfo>> GetAllPages(string url)
    {
        var doc = await GetGbkDocument(ToCatalogUrl(url));

        // Chapter list lives in <div class="catalog" id="catalog"> as
        // <ul><li><a href="https://www.69shuba.com/txt/<id>/<chapterid>">title</a></li>...
        // The catalog also contains an <h1 class="muluh1"><a> heading link, but that
        // anchor is not inside an <li>, so descending through <li> excludes it.
        var catalog = doc.DocumentNode.Descendants("div")
            .FirstOrDefault(d => d.GetAttributeValue("id", "").Equals("catalog"));

        if (catalog == null)
            return new List<ChapterLinkInfo>();

        return catalog.Descendants("li")
            .SelectMany(li => li.Descendants("a"))
            .Where(a => string.IsNullOrWhiteSpace(a.GetAttributeValue("href", "")) == false)
            .Select(a => new ChapterLinkInfo
            {
                Info = HttpUtility.HtmlDecode(a.InnerText).Trim(),
                Url = MakeAbsolute(url, a.GetAttributeValue("href", ""))
            })
            // The site lists newest -> oldest; reverse to chronological (oldest first).
            .Reverse()
            .ToList();
    }

    // 69shuba has no volume structure -- chapters are a single flat catalog.
    public override Task<IList<VolumeLinkInfo>> GetVolumePages(string url) =>
        throw new NotSupportedException("This website does not support voluming!!");

    public override async Task<string> GetNovelName(string url)
    {
        var doc = await GetGbkDocument(ToCatalogUrl(url));

        var heading = HttpUtility.HtmlDecode(doc.DocumentNode.Descendants("h1")
            .FirstOrDefault(n => n.GetAttributeValue("class", "").Contains("muluh1"))?
            .InnerText ?? "").Trim();

        // The catalog heading reads "<novel name>最新章节" ("latest chapters");
        // strip that trailing suffix to get the clean title.
        return Regex.Replace(heading, "最新章节$", "").Trim();
    }

    public override async Task<Chapter> GetChapter(string url)
    {
        var doc = await GetGbkDocument(url);

        // Body + title live inside <div class="txtnav">.
        var txtnav = doc.DocumentNode.Descendants("div")
            .FirstOrDefault(d => d.GetAttributeValue("class", "").Split(' ').Contains("txtnav"));

        var title = HttpUtility.HtmlDecode(
            txtnav?.Descendants("h1").FirstOrDefault()?.InnerText ?? "").Trim();

        // The prose sits as direct #text children of <div class="txtnav">, with
        // paragraphs separated by <br><br>. The chapter heading (<h1>), the info line
        // (<div class="txtinfo">) and the ad blocks (<div id="txtright">,
        // <div class="bottom-ad">, <script>) are all element children -- so taking
        // only the direct text nodes cleanly excludes navigation/ads/scripts.
        var body = (txtnav?.ChildNodes ?? Enumerable.Empty<HtmlNode>())
            .Where(n => n.Name.Equals("#text"))
            .Select(n => HttpUtility.HtmlDecode(n.InnerText).Trim())
            .Where(s => string.IsNullOrWhiteSpace(s) == false)
            .ToList();

        return new Chapter
        {
            Title = title,
            Body = body
        };
    }

    // Fetches raw bytes and decodes them with the correct (GBK) encoding, then loads
    // the result into an HtmlDocument. See the ENCODING NOTE at the top of the class.
    private static async Task<HtmlDocument> GetGbkDocument(string url)
    {
        // www.69shuba.com is behind Cloudflare (403 to non-browser clients); the bare
        // domain serves the same pages, so always fetch from there. The catalog also links
        // chapters via the www host, so normalizing here fixes chapter fetches too.
        url = url.Replace("://www.69shuba.com", "://69shuba.com");

        Console.WriteLine($"Getting {url}");
        var bytes = await FetchBytesViaCurl(url);

        var html = DetectEncoding(bytes).GetString(bytes);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    // Reads the declared <meta charset> from the head (as ASCII, which is safe for the
    // charset token itself) and falls back to GBK, which is what 69shuba actually uses.
    private static Encoding DetectEncoding(byte[] bytes)
    {
        var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 1024));
        var match = Regex.Match(head, @"charset=[""']?\s*([\w-]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            try
            {
                return Encoding.GetEncoding(match.Groups[1].Value);
            }
            catch
            {
                // Unknown/unsupported charset name -> fall through to GBK.
            }
        }

        return Encoding.GetEncoding("gbk");
    }

    // Turns a book landing URL (…/book/<id>.htm) into the chapter-catalog URL (…/book/<id>/),
    // which is where the full chapter list (<div id="catalog">) lives. The .htm page only shows
    // a few "latest" chapters and has no #catalog.
    private static string ToCatalogUrl(string url)
    {
        var m = Regex.Match(url, @"/book/(\d+)");
        if (m.Success == false)
            return url;

        var uri = new Uri(url);
        return $"{uri.Scheme}://{uri.Host}/book/{m.Groups[1].Value}/";
    }

    // 69shuba currently uses absolute hrefs, but keep this defensive in case relative
    // links appear: prepend scheme + host from the page url.
    private static string MakeAbsolute(string pageUrl, string href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return href;

        if (href.StartsWith("http://") || href.StartsWith("https://"))
            return href;

        var uri = new Uri(pageUrl);
        var baseUrl = uri.Scheme + "://" + uri.Host;
        return href.StartsWith("/") ? baseUrl + href : baseUrl + "/" + href;
    }

    // 69shuba's Cloudflare enforces a per-IP request-RATE limit over a rolling time window
    // (not just a concurrency cap). BatchProcessor fires up to 100 chapters in parallel, so we:
    //   * cap simultaneous curl calls (FetchGate), independent of the batch size,
    //   * pace request STARTS to a minimum interval so the sustained rate stays under the limit, and
    //   * retry transient failures (429/dropped connection) with exponential backoff that
    //     outlasts the rate-limit cooldown (which is longer than a few hundred ms).
    private const int MaxConcurrentFetches = 3;
    private const int MaxAttempts = 8;
    private static readonly TimeSpan MinRequestSpacing = TimeSpan.FromMilliseconds(350);
    private static readonly SemaphoreSlim FetchGate = new(MaxConcurrentFetches);
    private static readonly SemaphoreSlim PaceLock = new(1);
    private static DateTime _lastRequestStart = DateTime.MinValue;

    // Fetches raw bytes via the system curl (see NETWORK NOTE at the top of the class),
    // throttled, paced and retried.
    private static async Task<byte[]> FetchBytesViaCurl(string url)
    {
        await FetchGate.WaitAsync();
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                await PaceAsync();
                var (exitCode, bytes, error) = await RunCurl(url);
                if (exitCode == 0 && bytes.Length > 0)
                    return bytes;

                if (attempt >= MaxAttempts)
                    throw new Exception(
                        $"curl failed (exit {exitCode}) for {url} after {attempt} attempts: {error}");

                // Exponential backoff (2s, 4s, 8s ... capped at 60s) to outlast the
                // rate-limit cooldown; pausing here also lowers the overall request rate.
                var backoffSeconds = Math.Min(60, Math.Pow(2, attempt));
                await Task.Delay(TimeSpan.FromSeconds(backoffSeconds));
            }
        }
        finally
        {
            FetchGate.Release();
        }
    }

    // Ensures request starts are spaced by at least MinRequestSpacing across all threads.
    private static async Task PaceAsync()
    {
        TimeSpan wait;
        await PaceLock.WaitAsync();
        try
        {
            var since = DateTime.UtcNow - _lastRequestStart;
            wait = MinRequestSpacing - since;
            if (wait < TimeSpan.Zero)
                wait = TimeSpan.Zero;
            _lastRequestStart = DateTime.UtcNow + wait;
        }
        finally
        {
            PaceLock.Release();
        }

        if (wait > TimeSpan.Zero)
            await Task.Delay(wait);
    }

    // curl -s (silent) -L (follow redirects) -A (User-Agent) -e (Referer) --fail
    // (non-2xx -> non-zero exit code so we can retry).
    private static async Task<(int exitCode, byte[] bytes, string error)> RunCurl(string url)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "curl",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-s");
        psi.ArgumentList.Add("-L");
        psi.ArgumentList.Add("--fail");
        psi.ArgumentList.Add("-A");
        psi.ArgumentList.Add(UserAgent);
        psi.ArgumentList.Add("-e");
        psi.ArgumentList.Add(Referer);
        psi.ArgumentList.Add(url);

        using var process = Process.Start(psi)
                            ?? throw new Exception($"Could not start curl for {url}");

        using var buffer = new MemoryStream();
        var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(buffer);
        var stderr = await process.StandardError.ReadToEndAsync();
        await stdoutTask;
        await process.WaitForExitAsync();

        return (process.ExitCode, buffer.ToArray(), stderr);
    }
}
