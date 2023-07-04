using logic.Scrapping.Scrappers;
using logic.WebSites.Sites;

namespace logic.Automating.Scrapers;

public class RiwyatAutomateScrape : AutomateScrape
{
    public RiwyatAutomateScrape(string url, string dir, string fontSize, int whiteLinesBetweenLines,
     int numberOfChaptersPerFile) : base(url, dir, fontSize, whiteLinesBetweenLines, numberOfChaptersPerFile)
    {
    }

    public override async Task<IEnumerable<string>> GetChapter(string url)
    {
        var scrapper = new WebScrapper<IEnumerable<string>>(new Riwyat(url));
        return await scrapper.GetData();
    }

    public override async Task<IList<(string url, string title)>> GetPages()
    {
        var pagesScrapper = new WebScrapper<IEnumerable<(string url, string title)>>(new RiwyatPages(_url));
        return (await pagesScrapper.GetData()).ToList();
    }
}