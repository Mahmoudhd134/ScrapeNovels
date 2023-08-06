using Application.DTOs;
using Application.Helpers;
using Application.Interfaces;
using Domain.Abstractions;

namespace Application.Scrapers;

public sealed class ToPdfScraper : Scraper
{
    private readonly string _dir;
    private readonly IPdfMaker _pdfMaker;

    private ToPdfScraper(WebSite webSite) : base(webSite)
    {
    }

    public ToPdfScraper(WebSite webSite, IPdfMaker pdfMaker, string dir) : base(webSite)
    {
        _pdfMaker = pdfMaker;
        _dir = dir;
    }

    public override async Task StartWithNoVolumesSeparators(int numberOfChaptersPerFile)
    {
        UtilityFunctions.CheckDirectory(_dir);
        var pages = await WebSite.GetAllPages();
        var all = new List<ChapterDto>();
        var chapters = new List<ChapterDto>();
        var i = 0;
        string fileName;
        for (; i < pages.Count; i++)
        {
            if (i % numberOfChaptersPerFile == 0 && i != 0)
            {
                fileName = $@"{_dir}\{i + 1 - numberOfChaptersPerFile}-{i}.pdf";
                await _pdfMaker.MakeFromChapters(chapters, fileName);
                all.AddRange(chapters);
                chapters = new List<ChapterDto>();
            }

            var page = pages[i];

            var chapter = await WebSite.GetChapter(page.Url);

            chapters.Add(new ChapterDto
            {
                Title = $"{page.Info}    {chapter.Title}",
                Body = chapter.Body
            });
            UtilityFunctions.PrintProgress(i + 1, pages.Count, false, false);
        }

        fileName =
            $@"{_dir}\{i + 1 - (i % numberOfChaptersPerFile == 0 ? numberOfChaptersPerFile : i % numberOfChaptersPerFile)}-{i}.pdf";
        await _pdfMaker.MakeFromChapters(chapters, fileName);

        UtilityFunctions.PrintProgress(i, pages.Count, false);

        await JsonUtilityFunctions.WriteToFile(all,
            Path.Combine(_dir, UtilityFunctions.MakeValidFileNameFromString(await WebSite.GetNovelName()) + ".json"));
    }

    public override async Task StartWithVolumesSeparators()
    {
        UtilityFunctions.CheckDirectory(_dir);
        var volumes = await WebSite.GetVolumePages();
        var lastChapter = 0;
        var novel = new Novel { Name = await WebSite.GetNovelName() };
        for (var i = 0; i < volumes.Count; i++)
        {
            var chapters = new List<ChapterDto>();
            var chaptersCount = volumes[i].Chapters.Count();
            for (var j = 0; j < chaptersCount; j++)
            {
                var chapterLink = volumes[i].Chapters.ElementAt(j);
                var chapter = await WebSite.GetChapter(chapterLink.Url);
                chapters.Add(new ChapterDto
                {
                    Title = $"{chapterLink.Info}   {chapter.Title}",
                    Body = chapter.Body
                });
                UtilityFunctions.PrintProgress(j + 1, chaptersCount, false, false);
            }

            novel.Volumes.Add(new VolumeDto
            {
                Title = volumes[i].Title,
                Chapters = chapters
            });

            await HandleFiles(UtilityFunctions.MakeValidFileNameFromString(volumes[i].Title), i, lastChapter,
                chaptersCount, chapters, novel.Volumes.Last());
            lastChapter += chaptersCount;

            PrintProgress(i, volumes.Count);
        }

        await JsonUtilityFunctions.WriteToFile(novel,
            Path.Combine(_dir, UtilityFunctions.MakeValidFileNameFromString(novel.Name) + ".json"));
    }

    private async Task HandleFiles(string volumeName, int volumeIndex, int lastChapter, int chaptersCount,
        IEnumerable<ChapterDto> chapters, VolumeDto volumeDto)
    {
        var guid = Guid.NewGuid();
        var fileName = $"{volumeIndex + 1}.{volumeName}_from-{lastChapter + 1}_to-{lastChapter + chaptersCount}";

        var pdfFileName = $@"{_dir}\{guid}.pdf";
        var newPdfFileName = $@"{_dir}\{fileName}.pdf";
        var jsonFileName = $@"{_dir}\JSONs\{fileName}.json";

        await _pdfMaker.MakeFromChapters(chapters, pdfFileName);

        File.Copy(pdfFileName, newPdfFileName, true);
        File.Delete(pdfFileName);

        UtilityFunctions.CheckDirectory(Path.Combine(_dir, "JSONs"));
        await JsonUtilityFunctions.WriteToFile(volumeDto, jsonFileName);
    }

    private static void PrintProgress(int i, int volumesCount)
    {
        Console.WriteLine(UtilityFunctions.Repeat("=", 100));
        Console.WriteLine($"Volume No.({i + 1}) finished!!");
        Console.WriteLine(i < volumesCount - 1 ? $"Start Volume No.({i + 2})!!" : "Finished!!!");
        Console.WriteLine(UtilityFunctions.Repeat("=", 100));
    }
}