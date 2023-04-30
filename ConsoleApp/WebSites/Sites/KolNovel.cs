using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;

namespace ConsoleApp.WebSites.Sites
{
    public class KolNovel : WebSite<IEnumerable<string>>
    {
        public KolNovel(string url) : base(url)
        {
        }

        public override  Task<IEnumerable<string>> Parse(string html)
        {
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            var titles = GetTitles(htmlDocument);

            var spannedClassesAndIds = GetClassAndIdsInStyleTags(htmlDocument);
            var chapter = htmlDocument.DocumentNode.Descendants("p")
                .Where(n => spannedClassesAndIds.Contains(n.GetAttributeValue("class", "GGGAAAXXX")) == false)
                .Select(n => HttpUtility.HtmlDecode(n.InnerText))
                .Where(s => string.IsNullOrWhiteSpace(s) == false);

            var result = new List<string>(){titles};
            result.AddRange(chapter);
            return Task.FromResult<IEnumerable<string>>(result);
        }

        private string GetTitles(HtmlDocument htmlDocument)
        {
            var entryTitle = htmlDocument.DocumentNode.Descendants("h1")
                .FirstOrDefault(h1 => h1.GetAttributeValue("class", "NO").Equals("entry-title"))?
                .InnerText ?? "";

            var mainTitle = htmlDocument.DocumentNode.Descendants("div")
                .FirstOrDefault(div => div.GetAttributeValue("class", "NO").Equals("cat-series"))?
                .InnerText ?? "";

            var anotherTwoTitles = string.Join(" , ",htmlDocument.DocumentNode.Descendants("p")
                .Where(p => p.GetAttributeValue("style", "NO").Equals("text-align: center;") ||
                            p.GetAttributeValue("style", "NO").Equals("text-align: left"))
                .Select(p => p.InnerText));

            return HttpUtility.HtmlDecode($"{entryTitle}\n{mainTitle}\n{anotherTwoTitles}");
        }

        private IEnumerable<string> GetClassAndIdsInStyleTags(HtmlDocument htmlDocument)
        {
            var styleTags = string.Join("\n", htmlDocument.DocumentNode.Descendants("style")
                .Select(n => n.InnerText));
            var filter = new Regex(@"[.#][^\s{]+");
            return filter.Matches(styleTags).Select(m => string.Join("", m.Value.Skip(1)));
        }
    }
}