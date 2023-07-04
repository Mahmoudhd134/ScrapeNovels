using HtmlAgilityPack;

namespace logic.WebSites.Sites;

public class KolNovelPages : WebSite<IEnumerable<(string url, string title)>>
{
    public KolNovelPages(string url) : base(url)
    {
    }

    public override Task<IEnumerable<(string url, string title)>> Parse(string html)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(html);
        var links = htmlDocument.DocumentNode.Descendants("div")
            .Where(n => n.GetAttributeValue("class", "").Equals("bixbox bxcl epcheck"))
            .SelectMany(d => d.Descendants("li"))
            .SelectMany(li => li.Descendants("a"))
            .Where(a => a.GetAttributeValue("href", "").Equals("") == false)
            .Select(a => (a.GetAttributeValue("href", ""), a.InnerText))
            .Where(h => string.IsNullOrWhiteSpace(h.Item1 ?? "") == false).Reverse();
        return Task.FromResult<IEnumerable<(string, string)>>(links);
    }
}