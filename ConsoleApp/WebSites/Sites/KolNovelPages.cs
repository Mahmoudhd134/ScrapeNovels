using HtmlAgilityPack;

namespace ConsoleApp.WebSites.Sites;

public class KolNovelPages : WebSite<IEnumerable<(string page, string title)>>
{
    public KolNovelPages(string url) : base(url)
    {
    }

    public override Task<IEnumerable<(string page, string title)>> Parse(string html)
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