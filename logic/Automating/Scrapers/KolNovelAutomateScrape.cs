using logic.Scrapping.Scrappers;
using logic.WebSites.Sites;

namespace logic.Automating.Scrapers;

public class KolNovelAutomateScrape : AutomateScrape
{
    public KolNovelAutomateScrape(string url, string dir, string fontSize, int whiteLinesBetweenLines,
        int numberOfChaptersPerFile) : base(url, dir, fontSize, whiteLinesBetweenLines, numberOfChaptersPerFile)
    {

    }

    public override async Task<IEnumerable<string>> GetChapter(string url)
    {
        var chapterScrapper = new WebScrapper<IEnumerable<string>>(new KolNovel(url));
        return await chapterScrapper.GetData();
    }

    public override async Task<IList<(string url, string title)>> GetPages()
    {
        var pagesScrapper = new WebScrapper<IEnumerable<(string url, string title)>>(new KolNovelPages(_url));
        return (await pagesScrapper.GetData()).ToList();
    }
}