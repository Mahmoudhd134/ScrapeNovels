using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Application.Helpers;
using Domain.NovelModels;
using Domain.Websites;
using HtmlAgilityPack;

namespace Application.Implementation.NovelWebsites;

// The site behind "Riwyat" moved to cenele.com (a WordPress "Madara"/NovelHub theme).
public class Riwyat : NovelWebsite
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
    private static readonly HttpClient HttpClient = new();

    public override async Task<IList<ChapterLinkInfo>> GetAllPages(string url)
    {
        var doc = await GetChaptersDocument(url);

        return doc.DocumentNode.Descendants("li")
            .Where(li => li.GetAttributeValue("class", "").Contains("wp-manga-chapter"))
            .Select(ChapterFromLi)
            .Reverse()
            .ToList();
    }

    public override async Task<IList<VolumeLinkInfo>> GetVolumePages(string url)
    {
        var doc = await GetChaptersDocument(url);

        var container = doc.DocumentNode.Descendants("ul")
            .FirstOrDefault(u => u.GetAttributeValue("class", "").Contains("main version-chap"));

        var volumes = new List<VolumeLinkInfo>();

        if (container != null)
        {
            volumes = container.Descendants("ul")
                .Where(u => u.GetAttributeValue("class", "").Contains("parent has-child"))
                .Select((v, i) =>
                {
                    var volumeTitle = HttpUtility.HtmlDecode(v.Descendants("a")
                        .FirstOrDefault(a => a.GetAttributeValue("class", "").Contains("has-child"))?
                        .InnerText.Trim() ?? $"Volume {i + 1}");

                    var chapters = v.Descendants("li")
                        .Where(li => li.GetAttributeValue("class", "").Contains("wp-manga-chapter"))
                        .Select(ChapterFromLi)
                        .Reverse()
                        .ToList();

                    return new VolumeLinkInfo
                    {
                        Title = volumeTitle,
                        Chapters = chapters
                    };
                })
                .ToList();
        }

        // Fallback: no real volume grouping was found, wrap every chapter into a single
        // volume so the volume-based pipeline keeps working.
        if (volumes.Count == 0)
        {
            var allChapters = doc.DocumentNode.Descendants("li")
                .Where(li => li.GetAttributeValue("class", "").Contains("wp-manga-chapter"))
                .Select(ChapterFromLi)
                .Reverse()
                .ToList();

            if (allChapters.Count > 0)
                volumes.Add(new VolumeLinkInfo
                {
                    Title = "All Chapters",
                    Chapters = allChapters
                });

            return volumes;
        }

        volumes.Reverse();
        return volumes;
    }

    public override async Task<string> GetNovelName(string url)
    {
        var doc = await GetDocument(url);
        return HttpUtility.HtmlDecode(doc.DocumentNode.Descendants("h1")
            .FirstOrDefault(h1 => h1.GetAttributeValue("class", "").Contains("nhv-novel-title"))?
            .InnerText.Trim());
    }

    public override async Task<Chapter> GetChapter(string url)
    {
        var doc = await GetDocument(url);

        var volumeName = doc.DocumentNode.Descendants("div")
            .FirstOrDefault(d => d.GetAttributeValue("class", "").Contains("nhv-reading-volume-name"))?
            .InnerText.Trim() ?? "";

        var chapterName = doc.DocumentNode.Descendants("h3")
            .FirstOrDefault(h3 => h3.GetAttributeValue("class", "").Contains("chapter-name"))?
            .InnerText.Trim() ?? "";

        var title = Normalize(HttpUtility.HtmlDecode($"{chapterName}\n{volumeName}"));

        // Anti-scraping: decoy blocks/spans are hidden via CSS classes declared in <style> tags.
        var hiddenClasses = GetHiddenClasses(doc);

        // Prose is class-less <p> (older chapters) OR class-less <div> paragraphs inside a
        // ".markdown"/".md-content" panel (newer chapters). The <novel-chapter> element wraps
        // both; fall back to .reading-content only if it wasn't parsed as a container.
        var novelChapter = doc.DocumentNode.Descendants("novel-chapter").FirstOrDefault();
        var container = novelChapter != null && HasProse(novelChapter)
            ? novelChapter
            : doc.DocumentNode.Descendants("div")
                .FirstOrDefault(d => d.GetAttributeValue("class", "").Contains("reading-content"));

        var body = new List<string>();
        if (container != null)
        {
            body = container.Descendants()
                // A paragraph is a <p> or a <div> ...
                .Where(n => n.Name is "p" or "div")
                // ... that carries no class (promo/decoy/meta wrappers are classed) ...
                .Where(n => string.IsNullOrEmpty(n.GetAttributeValue("class", "")))
                // ... and is a leaf (no nested <p>/<div>), so wrapper panels aren't double-counted.
                .Where(n => n.ChildNodes.All(c => c.Name is not ("p" or "div")))
                // Skip anything that is itself hidden or nested inside a hidden/promo subtree.
                .Where(n => !IsHidden(n, hiddenClasses) &&
                            !n.Ancestors().Any(a => IsHidden(a, hiddenClasses)))
                // Read text only from non-hidden nodes so inline decoy spans are dropped.
                .Select(n => Normalize(HttpUtility.HtmlDecode(GetVisibleText(n, hiddenClasses))))
                .Where(t => string.IsNullOrWhiteSpace(t) == false)
                .ToList();
        }

        return new Chapter
        {
            Title = title,
            Body = body
        };
    }

    // Cleans scraped text: turns non-breaking / exotic spaces into a normal space, drops
    // zero-width and soft-hyphen junk, and collapses runs of spaces/tabs (newlines kept, so
    // the title's chapter/volume split survives). This removes the stray   (&nbsp;) that
    // HtmlDecode leaves in the prose.
    private static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? "";

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            switch (ch)
            {
                // Non-breaking / figure / narrow-no-break / word-joiner spaces -> plain space.
                case ' ' or ' ' or ' ' or '⁠':
                    sb.Append(' ');
                    break;
                // Junk: zero-width space, BOM/zero-width-no-break, soft hyphen -> drop.
                // (ZWNJ ‌ / ZWJ ‍ are kept -- they can be meaningful in Arabic.)
                case '​' or '﻿' or '­':
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }

        // Collapse runs of horizontal whitespace but preserve newlines.
        return Regex.Replace(sb.ToString(), @"[^\S\n]+", " ").Trim();
    }

    // True if the node contains at least one class-less leaf <p>/<div> paragraph (prose),
    // used to pick <novel-chapter> as the container in both the <p> and <div> chapter formats.
    private static bool HasProse(HtmlNode node) =>
        node.Descendants().Any(d =>
            d.Name is "p" or "div"
            && string.IsNullOrEmpty(d.GetAttributeValue("class", ""))
            && d.ChildNodes.All(c => c.Name is not ("p" or "div")));

    private static ChapterLinkInfo ChapterFromLi(HtmlNode li)
    {
        var aTag = li.Descendants("a").First();
        return new ChapterLinkInfo
        {
            Url = aTag.GetAttributeValue("href", "NO LINK FOUND #CUSTOM ERROR#"),
            Info = HttpUtility.HtmlDecode(aTag.InnerText.Trim())
        };
    }

    private static async Task<HtmlDocument> GetDocument(string url)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(await UtilityFunctions.GetHtmlFromUrl(url));
        return doc;
    }

    // The cenele.com (Madara/NovelHub theme) chapter list is loaded lazily through a POST-only
    // ajax endpoint, so a plain GET of the novel page only returns the latest few chapters. We
    // POST here and load the returned fragment, which is the classic Madara markup the parsers expect.
    private static async Task<HtmlDocument> GetChaptersDocument(string url)
    {
        var ajaxUrl = $"{url.TrimEnd('/')}/ajax/chapters/";

        using var request = new HttpRequestMessage(HttpMethod.Post, ajaxUrl);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Content = new StringContent("", Encoding.UTF8, "application/x-www-form-urlencoded");

        using var response = await HttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    // Collect every class/id whose CSS rule hides content. To avoid over-matching ancestor
    // selectors (e.g. ".reading-content .rcXXXX"), only the rightmost (key) selector token of
    // each hiding rule is treated as hidden.
    private static ISet<string> GetHiddenClasses(HtmlDocument doc)
    {
        var hidden = new HashSet<string>();

        var css = string.Join("\n", doc.DocumentNode.Descendants("style").Select(s => s.InnerText));
        var selectorToken = new Regex(@"[.#][\w-]+");

        foreach (var rawRule in css.Split('}'))
        {
            var braceIndex = rawRule.IndexOf('{');
            if (braceIndex < 0)
                continue;

            var selectorPart = rawRule.Substring(0, braceIndex);
            var declaration = Regex.Replace(rawRule.Substring(braceIndex + 1), @"\s+", "").ToLowerInvariant();

            if (IsHidingDeclaration(declaration) == false)
                continue;

            foreach (var selector in selectorPart.Split(','))
            {
                var tokens = selectorToken.Matches(selector);
                if (tokens.Count == 0)
                    continue;

                hidden.Add(tokens[tokens.Count - 1].Value.Substring(1));
            }
        }

        return hidden;
    }

    // IMPORTANT: only the "visually-hidden / off-screen" pattern counts as a decoy hide.
    // Plain display:none / visibility:hidden are used by many legitimate UI classes
    // (e.g. li.current, .site-footer, .entry-header) that appear on content wrappers, so
    // treating those as hidden would wrongly blacklist the whole reading container.
    private static bool IsHidingDeclaration(string declaration)
    {
        var offScreen = declaration.Contains("position:absolute");
        var tiny = declaration.Contains("width:1px") && declaration.Contains("height:1px");
        // "opacity:0" but NOT "opacity:0.8" etc.
        var transparent = declaration.Contains("opacity(0)") ||
                          Regex.IsMatch(declaration, @"opacity:0(?![.\d])");
        var clipped = declaration.Contains("clip:rect") || declaration.Contains("clip-path:inset");
        var indentedAway = declaration.Contains("text-indent:-");

        return (offScreen && tiny) ||
               (offScreen && transparent) ||
               (tiny && transparent) ||
               clipped ||
               indentedAway;
    }

    private static bool IsHidden(HtmlNode node, ISet<string> hiddenClasses)
    {
        if (node.Name is "style" or "script")
            return true;

        // Decoy blocks are injected as inert, non-indexable elements.
        if (node.Attributes.Contains("inert") || node.Attributes.Contains("data-nosnippet"))
            return true;

        var cls = node.GetAttributeValue("class", "");
        if (string.IsNullOrEmpty(cls))
            return false;

        // Ignore promotional blocks injected into the reading area.
        if (cls.Contains("nhv-reader-promo") || cls.Contains("nhv-reader-store"))
            return true;

        return cls.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(hiddenClasses.Contains);
    }

    // Concatenate text only from non-hidden nodes, recursing so inline hidden decoy spans
    // inside a paragraph are dropped.
    private static string GetVisibleText(HtmlNode node, ISet<string> hiddenClasses)
    {
        var sb = new StringBuilder();

        foreach (var child in node.ChildNodes)
        {
            if (IsHidden(child, hiddenClasses))
                continue;

            if (child.Name == "#text")
                sb.Append(child.InnerText);
            else
                sb.Append(GetVisibleText(child, hiddenClasses));
        }

        return sb.ToString();
    }
}
