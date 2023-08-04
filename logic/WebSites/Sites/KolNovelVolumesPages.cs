using HtmlAgilityPack;

namespace logic.WebSites.Sites;

public class KolNovelVolumesPages : WebSite<IEnumerable<VolumeLinkInfo>>
{
    public KolNovelVolumesPages(string url) : base(url)
    {
    }

    public override Task<IEnumerable<VolumeLinkInfo>> Parse(string html)
    {
        var doc = new HtmlDocument();
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
                    Title = a.InnerText,
                    Url = a.GetAttributeValue("href", "")
                });


            allVolumes.Add(new VolumeLinkInfo()
            {
                Title = content[i - 1].InnerText,
                Chapters = lis.Reverse()
            });
        }

        allVolumes.Reverse();
        return Task.FromResult<IEnumerable<VolumeLinkInfo>>(allVolumes);
    }
}