using System.Web;
using Application.Helpers;
using Domain.Abstractions;
using Domain.Models;
using HtmlAgilityPack;

namespace Application.WebSites;

public class Riwyat : WebSite
{
    public Riwyat(string baseUrl) : base(baseUrl)
    {
    }

    public override async Task<IList<ChapterLinkInfo>> GetAllPages()
    {
        var doc = new HtmlDocument();
        var html = await UtilityFunctions.GetHtmlFromUrl(BaseUrl);
        doc.LoadHtml(html);

        var allLi = doc.DocumentNode.Descendants("li")
            .Where(li => li.GetAttributeValue("class", "").Contains("wp-manga-chapter"));

        return allLi.Select(li =>
                new ChapterLinkInfo
                {
                    Url = li.Descendants("a").First().GetAttributeValue("href", "NO LINK FOUND #CUSTOM ERROR#"),
                    Info = li.InnerText
                })
            .Reverse()
            .ToList();
    }

    public override Task<IList<VolumeLinkInfo>> GetVolumePages()
    {
        throw new NotImplementedException();
    }

    public override Task<string> GetNovelName()
    {
        throw new NotImplementedException();
    }

    public override async Task<Chapter> GetChapter(string url)
    {
        var doc = new HtmlDocument();
        var html = await UtilityFunctions.GetHtmlFromUrl(url);
        doc.LoadHtml(html);

        var title = doc.DocumentNode.Descendants("div")
            .Single(d => d.GetAttributeValue("class", "").Equals("text-right"))
            .Descendants("h3")
            .First().InnerText;

        var chapter = doc.DocumentNode.Descendants("div")
            .First(d => d.GetAttributeValue("class", "").Equals("read-container"))
            .Descendants("p")
            .Select(c => HttpUtility.HtmlDecode(c.InnerText));

        return new Chapter
        {
            Title = title,
            Body = chapter
        };
    }
}