using System.Text.RegularExpressions;
using System.Web;
using Application.Helpers;
using Domain.Abstractions;
using Domain.Models;
using HtmlAgilityPack;

namespace Application.WebSites;

public class KolNovel : IWebSite
{
    public async Task<IList<ChapterLinkInfo>> GetAllPages(string url)
    {
        var htmlDocument = new HtmlDocument();
        var html = await UtilityFunctions.GetHtmlFromUrl(url);
        htmlDocument.LoadHtml(html);

        return htmlDocument.DocumentNode.Descendants("div")
            .Where(n => n.GetAttributeValue("class", "").Equals("bixbox bxcl epcheck"))
            .SelectMany(d => d.Descendants("li"))
            .SelectMany(li => li.Descendants("a"))
            .Where(a => a.GetAttributeValue("href", "").Equals("") == false)
            .Select(a => new ChapterLinkInfo()
            {
                Info = a.InnerText,
                Url = a.GetAttributeValue("href", "")
            })
            .Where(c => string.IsNullOrWhiteSpace(c.Url ?? "") == false)
            .Reverse()
            .ToList();
    }

    public async Task<IList<VolumeLinkInfo>> GetVolumePages(string url)
    {
        var doc = new HtmlDocument();
        var html = await UtilityFunctions.GetHtmlFromUrl(url);
        doc.LoadHtml(html);

        var content = doc.DocumentNode.Descendants("div")
            .Single(d => d.GetAttributeValue("class", "").Equals("bixbox bxcl epcheck"))
            .ChildNodes
            .Where(c => c.Name.Equals("#text") == false)
            .ToList();

        var allVolumes = new List<VolumeLinkInfo>();

        var spanIsThier = false;
        for (var i = 0; i < content.Count; i++)
        {
            if (content[i].Name.Equals("span"))
            {
                spanIsThier = true;
                continue;
            }

            if (spanIsThier == false)
                continue;

            spanIsThier = false;
            var lis = content[i].Descendants("li")
                .Select(li => li.Descendants("a").First())
                // .Where(a => a.GetAttributeValue("href", "").Equals("") == false)
                .Select(a => new ChapterLinkInfo()
                {
                    Info = a.InnerText,
                    Url = a.GetAttributeValue("href", "")
                });


            allVolumes.Add(new VolumeLinkInfo()
            {
                Title = content[i - 1].InnerText,
                Chapters = lis.Reverse()
            });
        }
        allVolumes.Reverse();

        return allVolumes;
    }

    public async Task<Chapter> GetChapter(string url)
    {
        var htmlDocument = new HtmlDocument();
        var html = await UtilityFunctions.GetHtmlFromUrl(url);
        htmlDocument.LoadHtml(html);

        var titles = GetTitles(htmlDocument);

        var spannedClassesAndIds = GetClassAndIdsInStyleTags(htmlDocument)
            .ToHashSet();
        var chapter = htmlDocument.DocumentNode.Descendants("p")
            .Where(n => spannedClassesAndIds.Contains(n.GetAttributeValue("class", "GGGAAAXXX")) == false)
            .Select(n => HttpUtility.HtmlDecode(n.InnerText))
            .Where(s => string.IsNullOrWhiteSpace(s) == false);

        return new Chapter
        {
            Title = titles,
            Body = chapter
        };
    }

    private string GetTitles(HtmlDocument htmlDocument)
    {
        var entryTitle = htmlDocument.DocumentNode.Descendants("h1")
            .FirstOrDefault(h1 => h1.GetAttributeValue("class", "NO").Equals("entry-title"))?
            .InnerText ?? "";

        var mainTitle = htmlDocument.DocumentNode.Descendants("div")
            .FirstOrDefault(div => div.GetAttributeValue("class", "NO").Equals("cat-series"))?
            .InnerText ?? "";

        return HttpUtility.HtmlDecode($"{entryTitle}\n{mainTitle}\n\n");
    }

    private IEnumerable<string> GetClassAndIdsInStyleTags(HtmlDocument htmlDocument)
    {
        var styleTags = string.Join("\n", htmlDocument.DocumentNode.Descendants("style")
            .Select(n => n.InnerText));
        var filter = new Regex(@"[.#][^\s{,]+");
        return filter.Matches(styleTags).Select(m => string.Join("", m.Value.Skip(1)));
    }
}