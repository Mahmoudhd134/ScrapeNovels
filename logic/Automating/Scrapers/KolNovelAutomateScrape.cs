using logic.Scrapping.Scrappers;
using logic.WebSites;
using logic.WebSites.Sites;

namespace logic.Automating.Scrapers;

public class KolNovelAutomateScrape : AutomateScrape
{
    public KolNovelAutomateScrape(string url, string dir, string fontSize, int whiteLinesBetweenLines) : base(url, dir,
        fontSize, whiteLinesBetweenLines)
    {
    }

    public override async Task<IList<VolumeLinkInfo>> GetVolumePages()
    {
        var pagesScrapper = new WebScrapper<IEnumerable<VolumeLinkInfo>>(new KolNovelVolumesPages(_url));
        return (await pagesScrapper.GetData()).ToList();
    }

    public override async Task<IEnumerable<string>> GetChapter(string url)
    {
        var chapterScrapper = new WebScrapper<IEnumerable<string>>(new KolNovel(url));
        return await chapterScrapper.GetData();
    }

    public override async Task<IList<ChapterLinkInfo>> GetAllPages()
    {
        var pagesScrapper = new WebScrapper<IEnumerable<ChapterLinkInfo>>(new KolNovelAllPages(_url));
        return (await pagesScrapper.GetData()).ToList();
    }
}