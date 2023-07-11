namespace logic.Automating;

public abstract class AutomateScrape
{
    protected readonly string _url;
    protected readonly string _dir;
    protected readonly string _fontSize;
    protected readonly int _whiteLinesBetweenLines;
    protected readonly int _numberOfChaptersPerFile;

    public AutomateScrape(string url, string dir, string fontSize, int whiteLinesBetweenLines,
        int numberOfChaptersPerFile)
    {
        _url = url;
        _dir = dir;
        _fontSize = fontSize;
        _whiteLinesBetweenLines = whiteLinesBetweenLines;
        _numberOfChaptersPerFile = numberOfChaptersPerFile;
    }

    public abstract Task<IList<(string url, string title)>> GetPages();
    public abstract Task<IEnumerable<string>> GetChapter(string url);

    public virtual async Task Start()
    {
        UtilityFunctions.CheckDirectory(_dir);
        var pages = await GetPages();
        var chapters = new List<IEnumerable<string>>();
        var i = 0;
        string fileName;
        for (; i < pages.Count; i++)
        {
            if (i % _numberOfChaptersPerFile == 0 && i != 0)
            {
                fileName = $@"{_dir}\HTMLs\{i + 1 - _numberOfChaptersPerFile}-{i}.html";
                await UtilityFunctions.Write(chapters, fileName, _whiteLinesBetweenLines, _fontSize, _dir);
                chapters = new List<IEnumerable<string>>();
            }

            var page = pages[i];

            var chapter = (await GetChapter(page.url))
                .Append(UtilityFunctions.Repeat("-", 100)).ToList().Prepend(page.title + "\n");


            chapters.Add(chapter);
            UtilityFunctions.PrintProgress(i + 1, pages.Count, false);
        }

        fileName =
            $@"{_dir}\HTMLs\{i + 1 - (i % _numberOfChaptersPerFile == 0 ? _numberOfChaptersPerFile : i % _numberOfChaptersPerFile)}-{i}.html";
        await UtilityFunctions.Write(chapters, fileName, _whiteLinesBetweenLines, _fontSize, _dir);
        UtilityFunctions.PrintProgress(i, pages.Count);
    }
}