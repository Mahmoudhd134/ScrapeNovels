using logic.Scrapping.Scrappers;
using logic.WebSites;
using logic.WebSites.Sites;

namespace logic.Automating.Scrapers;

public class RiwyatAutomateScrape : AutomateScrape
{
    public RiwyatAutomateScrape(string url, string dir, string fontSize, int whiteLinesBetweenLines) : base(url, dir,
        fontSize, whiteLinesBetweenLines)
    {
    }

    public override Task<IList<VolumeLinkInfo>> GetVolumePages()
    {
        throw new NotImplementedException();
    }

    public override async Task<IEnumerable<string>> GetChapter(string url)
    {
        var scrapper = new WebScrapper<IEnumerable<string>>(new Riwyat(url));
        return await scrapper.GetData();
    }

    public override async Task<IList<ChapterLinkInfo>> GetAllPages()
    {
        var pagesScrapper = new WebScrapper<IEnumerable<ChapterLinkInfo>>(new RiwyatPages(_url));
        return (await pagesScrapper.GetData()).ToList();
    }
}