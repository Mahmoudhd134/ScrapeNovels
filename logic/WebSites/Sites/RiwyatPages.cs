using HtmlAgilityPack;

namespace logic.WebSites.Sites;

public class RiwyatPages : WebSite<IEnumerable<ChapterLinkInfo>>
{
    public RiwyatPages(string url) : base(url)
    {
    }

    public override Task<IEnumerable<ChapterLinkInfo>> Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var allLi = doc.DocumentNode.Descendants("li")
            .Where(li => li.GetAttributeValue("class", "").Contains("wp-manga-chapter"));

        var links = allLi.Select(li =>
                new ChapterLinkInfo()
                {
                    Url = li.Descendants("a").First().GetAttributeValue("href", "NO LINK FOUND #CUSTOM ERROR#"),
                    Title = li.InnerText
                })
            .Reverse();

        return Task.FromResult(links);
    }
}