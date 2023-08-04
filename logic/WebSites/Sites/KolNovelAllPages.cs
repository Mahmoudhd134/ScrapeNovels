using HtmlAgilityPack;

namespace logic.WebSites.Sites;

public class KolNovelAllPages : WebSite<IEnumerable<ChapterLinkInfo>>
{
    public KolNovelAllPages(string url) : base(url)
    {
    }

    public override Task<IEnumerable<ChapterLinkInfo>> Parse(string html)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(html);
        var links = htmlDocument.DocumentNode.Descendants("div")
            .Where(n => n.GetAttributeValue("class", "").Equals("bixbox bxcl epcheck"))
            .SelectMany(d => d.Descendants("li"))
            .SelectMany(li => li.Descendants("a"))
            .Where(a => a.GetAttributeValue("href", "").Equals("") == false)
            .Select(a => new ChapterLinkInfo()
            {
                Title = a.InnerText,
                Url = a.GetAttributeValue("href", "")
            })
            .Where(c => string.IsNullOrWhiteSpace(c.Url ?? "") == false).Reverse();
        return Task.FromResult(links);
    }
}