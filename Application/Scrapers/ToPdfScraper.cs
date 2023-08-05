using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Application.Helpers;
using Domain.Abstractions;
using Domain.Models;

namespace Application.Scrapers;

public sealed class ToPdfScraper : Scraper
{
    public string Url { get; set; }
    public string Dir { get; set; }
    public string FontSize { get; set; }
    public int WhiteLinesBetweenLines { get; set; }

    public ToPdfScraper(IWebSite webSite) : base(webSite)
    {
    }

    public override async Task StartWithNoVolumesSeparators(int numberOfChaptersPerFile)
    {
        UtilityFunctions.CheckDirectory(Dir);
        var pages = await WebSite.GetAllPages(Url);
        var chapters = new List<Chapter>();
        var i = 0;
        string fileName;
        for (; i < pages.Count; i++)
        {
            if (i % numberOfChaptersPerFile == 0 && i != 0)
            {
                fileName = $@"{Dir}\HTMLs\{i + 1 - numberOfChaptersPerFile}-{i}.html";
                await UtilityFunctions.Write(chapters, fileName, WhiteLinesBetweenLines, FontSize,
                    Path.Combine(Dir, "PDFs"));
                chapters = new List<Chapter>();
            }

            var page = pages[i];

            var chapter = await WebSite.GetChapter(page.Url);
            chapter.Title = $"{page.Info}    {chapter.Title}";

            chapters.Add(chapter);
            UtilityFunctions.PrintProgress(i + 1, pages.Count, false, false);
        }

        fileName =
            $@"{Dir}\HTMLs\{i + 1 - (i % numberOfChaptersPerFile == 0 ? numberOfChaptersPerFile : i % numberOfChaptersPerFile)}-{i}.html";
        await UtilityFunctions.Write(chapters, fileName, WhiteLinesBetweenLines, FontSize, Path.Combine(Dir, "PDFs"));
        UtilityFunctions.PrintProgress(i, pages.Count, false, true);
    }

    public override async Task StartWithVolumesSeparators()
    {
        UtilityFunctions.CheckDirectory(Dir);
        var volumes = await WebSite.GetVolumePages(Url);
        var last = 0;
        for (var i = 0; i < volumes.Count; i++)
        {
            var chapters = new List<Chapter>();
            var chaptersCount = volumes[i].Chapters.Count();
            for (var j = 0; j < chaptersCount; j++)
            {
                var chapterLink = volumes[i].Chapters.ElementAt(j);
                chapters.Add(await WebSite.GetChapter(chapterLink.Url));
                chapters.Last().Title = $"{chapterLink.Info}   {chapters.Last().Title}";
                UtilityFunctions.PrintProgress(j + 1, chaptersCount, false, false);
            }

            var customModel = new
            {
                VolumeTitle = volumes[i].Title,
                Chapters = chapters
            };
            var options = new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true
            };
            var jsonContent = JsonSerializer.Serialize(customModel, options);

            var guid = Guid.NewGuid();
            var volumeName = UtilityFunctions.MakeValidFileNameFromString(volumes[i].Title);
            var fileName = $"{i + 1}.{volumeName}_from-{last + 1}_to-{last + chaptersCount}";

            var htmlFileName = $@"{Dir}\HTMLs\{guid}.html";
            var pdfFileName = $@"{Dir}\PDFs\{guid}.pdf";

            var newHtmlFileName = $@"{Dir}\HTMLs\{fileName}.html";
            var newPdfFileName = $@"{Dir}\PDFs\{fileName}.pdf";
            var jsonFileName = $@"{Dir}\JSONs\{fileName}.json";

            last += chaptersCount;

            await UtilityFunctions.Write(chapters, htmlFileName, WhiteLinesBetweenLines, FontSize,
                Path.Combine(Dir, "PDFs"));

            File.Copy(htmlFileName, newHtmlFileName, true);
            File.Copy(pdfFileName, newPdfFileName, true);
            UtilityFunctions.CheckDirectory(Path.Combine(Dir,"JSONs"));
            await File.WriteAllTextAsync(jsonFileName, jsonContent);

            File.Delete(htmlFileName);
            File.Delete(pdfFileName);

            Console.WriteLine(UtilityFunctions.Repeat("=", 100));
            Console.WriteLine($"Volume No.({i + 1}) finished!!");
            Console.WriteLine(i < volumes.Count - 1 ? $"Start Volume No.({i + 2})!!" : "Finished!!!");
            Console.WriteLine(UtilityFunctions.Repeat("=", 100));
        }
    }
}