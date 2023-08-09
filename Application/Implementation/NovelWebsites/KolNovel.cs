using System.Text.RegularExpressions;
using System.Web;
using Application.Helpers;
using Domain.NovelModels;
using Domain.Websites;
using HtmlAgilityPack;

namespace Application.Implementation.NovelWebsites;

public class KolNovel : NovelWebsite
{
    public override async Task<IList<ChapterLinkInfo>> GetAllPages(string url)
    {
        var htmlDocument = await GetDocument(url);

        return htmlDocument.DocumentNode.Descendants("div")
            .Where(n => n.GetAttributeValue("class", "").Equals("bixbox bxcl epcheck"))
            .SelectMany(d => d.Descendants("li"))
            .SelectMany(li => li.Descendants("a"))
            .Where(a => a.GetAttributeValue("href", "").Equals("") == false)
            .Select(a => new ChapterLinkInfo
            {
                Info = a.InnerText,
                Url = a.GetAttributeValue("href", "")
            })
            .Where(c => string.IsNullOrWhiteSpace(c.Url ?? "") == false)
            .Reverse()
            .ToList();
    }

    public override async Task<IList<VolumeLinkInfo>> GetVolumePages(string url)
    {
        var doc = await GetDocument(url);

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
                .Select(a => new ChapterLinkInfo
                {
                    Info = a.InnerText,
                    Url = a.GetAttributeValue("href", "")
                });


            allVolumes.Add(new VolumeLinkInfo
            {
                Title = content[i - 1].InnerText,
                Chapters = lis.Reverse().ToList()
            });
        }

        allVolumes.Reverse();

        return allVolumes;
    }

    public override async Task<string> GetNovelName(string url)
    {
        return (await GetDocument(url)).DocumentNode.Descendants("h1")
            .Single(h1 => h1.GetAttributeValue("class", "").Equals("entry-title"))
            .InnerText;
    }


    public override async Task<Chapter> GetChapter(string url)
    {
        var htmlDocument = await GetDocument(url);

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
            Body = chapter.ToList()
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

    private async Task<HtmlDocument> GetDocument(string url)
    {
        var htmlDocument = new HtmlDocument();
        var html = await UtilityFunctions.GetHtmlFromUrl(url);
        htmlDocument.LoadHtml(html);
        return htmlDocument;
    }
}