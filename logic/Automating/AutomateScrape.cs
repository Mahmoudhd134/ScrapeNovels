using logic.WebSites;

namespace logic.Automating;

public abstract class AutomateScrape
{
    protected readonly string _url;
    protected readonly string _dir;
    protected readonly string _fontSize;
    protected readonly int _whiteLinesBetweenLines;

    public AutomateScrape(string url, string dir, string fontSize, int whiteLinesBetweenLines)
    {
        _url = url;
        _dir = dir;
        _fontSize = fontSize;
        _whiteLinesBetweenLines = whiteLinesBetweenLines;
    }

    public abstract Task<IList<ChapterLinkInfo>> GetAllPages();
    public abstract Task<IList<VolumeLinkInfo>> GetVolumePages();
    public abstract Task<IEnumerable<string>> GetChapter(string url);

    public virtual async Task StartWithNoVolumesSeparators(int numberOfChaptersPerFile)
    {
        UtilityFunctions.CheckDirectory(_dir);
        var pages = await GetAllPages();
        var chapters = new List<IEnumerable<string>>();
        var i = 0;
        string fileName;
        for (; i < pages.Count; i++)
        {
            if (i % numberOfChaptersPerFile == 0 && i != 0)
            {
                fileName = $@"{_dir}\HTMLs\{i + 1 - numberOfChaptersPerFile}-{i}.html";
                await UtilityFunctions.Write(chapters, fileName, _whiteLinesBetweenLines, _fontSize, _dir);
                chapters = new List<IEnumerable<string>>();
            }

            var page = pages[i];

            var chapter = (await GetChapter(page.Url))
                .Append(UtilityFunctions.Repeat("-", 100)).ToList().Prepend(page.Title + "\n");


            chapters.Add(chapter);
            UtilityFunctions.PrintProgress(i + 1, pages.Count, false);
        }

        fileName =
            $@"{_dir}\HTMLs\{i + 1 - (i % numberOfChaptersPerFile == 0 ? numberOfChaptersPerFile : i % numberOfChaptersPerFile)}-{i}.html";
        await UtilityFunctions.Write(chapters, fileName, _whiteLinesBetweenLines, _fontSize, _dir);
        UtilityFunctions.PrintProgress(i, pages.Count);
    }

    public virtual async Task StartWithVolumesSeparators()
    {
        UtilityFunctions.CheckDirectory(_dir);
        var volumes = await GetVolumePages();
        var last = 0;
        foreach (var volume in volumes)
        {
            var chapters = new List<IEnumerable<string>>();
            foreach (var chapter in volume.Chapters)
            {
                chapters.Add((await GetChapter(chapter.Url))
                    .Append(UtilityFunctions.Repeat("-", 100))
                    .ToList()
                    .Prepend(chapter.Title + "\n"));
            }

            var fileName = $@"{_dir}\HTMLs\{last + 1}-{last += volume.Chapters.Count()}.html";
            await UtilityFunctions.Write(chapters, fileName, _whiteLinesBetweenLines, _fontSize, _dir);
            // UtilityFunctions.PrintProgress(i + 1, pages.Count, false);
        }
    }
}