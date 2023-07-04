using System.Web;
using HtmlAgilityPack;

namespace logic.WebSites.Sites;

public class Riwyat : WebSite<IEnumerable<string>>
{
    public Riwyat(string url) : base(url)
    {
    }

    public override Task<IEnumerable<string>> Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var chapter = doc.DocumentNode.Descendants("div")
        .First(d => d.GetAttributeValue("class","").Equals("read-container"))
        .Descendants("p")
        .Select(c => c.InnerText)
        .Select(HttpUtility.HtmlDecode);

        return Task.FromResult<IEnumerable<string>>(chapter);
        throw new NotImplementedException();
    }
}