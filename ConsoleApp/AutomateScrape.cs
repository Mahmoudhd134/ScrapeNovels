using ConsoleApp;
using System.Diagnostics;
using ConsoleApp.Scrapping.Scrappers;
using ConsoleApp.WebSites.Sites;

public class AutomateScrape
{
    private readonly string _url;
    private readonly string _dir;
    private readonly string _fontSize;
    private readonly int _whiteLinesBetweenLines;
    private readonly int _numberOfChaptersPerFile;

    public AutomateScrape(string url, string dir, string fontSize, int whiteLinesBetweenLines,
        int numberOfChaptersPerFile)
    {
        _url = url;
        _dir = dir;
        _fontSize = fontSize;
        _whiteLinesBetweenLines = whiteLinesBetweenLines;
        _numberOfChaptersPerFile = numberOfChaptersPerFile;
    }

    private void CheckDirectory(string dir)
    {
        if (Directory.Exists(dir) == false)
            Directory.CreateDirectory(dir);
    }

    private void CheckDirectory()
    {
        if (Directory.Exists(_dir) == false)
            Directory.CreateDirectory(_dir);
    }

    private void ConvertToPdf(string inputName, string outputName)
    {
        CheckDirectory(Path.GetDirectoryName(outputName));
        for (var i = 0; i < 3; i++)
        {
            var process = new Process();
            process.StartInfo.FileName = "wkhtmltopdf.exe";
            process.StartInfo.Arguments = $"\"{inputName}\" \"{outputName}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;

            Console.WriteLine("\nStart Converting...");

            process.Start();
            process.WaitForExit();

            if (process.ExitCode == 0)
                break;

            Console.WriteLine($"\n\nConversion failed with exit code {process.ExitCode}");
            Console.WriteLine(inputName + " >> " + outputName);
            Console.WriteLine($"trying number {i + 1} failed");
            if (i == 2)
            {
                throw new Exception($"\nConversion failed with exit code {process.ExitCode}\n{inputName} >> {outputName}");
            }
            Console.WriteLine("Retrying...");
            Console.WriteLine();
        }
    }

    private static string Repeat(string s, int count) => string.Concat(Enumerable.Repeat(s, count));

    private static void PrintProgress(int i, int count, bool newLine = true) =>
        Console.Write($"\rChapter ({i}/{count}) Done!!" + (newLine ? "\n" : ""));

    private async Task Write(IEnumerable<IEnumerable<string>> chapters, string fileName)
    {
        var refactored = chapters
            .Select(c => string.Join(Repeat("<br/>", _whiteLinesBetweenLines - 1),
                c.Select(l => $"<p>{l}</p>")));
        var s = "<!DOCTYPE html>" +
                "<html><head>" +
                "<meta charset=\"UTF-8\">" +
                "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">" +
                "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">" +
                "<title>Document</title>" +
                $"</head><body dir=\"rtl\" style=\"font-size:{_fontSize};\">" +
                string.Join("<p>" + Repeat("-", 200) + "</p>", refactored) +
                "</body></html>";

        await File.WriteAllTextAsync(fileName, s);

        var outputFileName = $"{_dir}\\PDFs\\{Path.GetFileNameWithoutExtension(fileName)}.pdf";
        ConvertToPdf(fileName, outputFileName);
        // File.Delete(fileName);
    }

    public async Task Start()
    {
        CheckDirectory();

        var pagesScrapper = new WebScrapper<IEnumerable<(string url, string title)>>(new KolNovelPages(_url));
        var pages = (await pagesScrapper.GetData()).ToList();
        var chapters = new List<IEnumerable<string>>();
        var i = 0;
        for (; i < pages.Count; i++)
        {
            if (i % _numberOfChaptersPerFile == 0 && i != 0)
            {
                var fileName = $@"{_dir}\HTMLs\{i + 1 - _numberOfChaptersPerFile}-{i}.html";
                CheckDirectory(Path.GetDirectoryName(fileName));
                await Write(chapters, fileName);
                chapters = new List<IEnumerable<string>>();
            }

            var page = pages[i];

            var chapterScrapper = new WebScrapper<IEnumerable<string>>(new KolNovel(page.url));

            var chapter = (await chapterScrapper.GetData()).Append(Repeat("-", 100)).ToList().Prepend(page.title + "\n");


            chapters.Add(chapter);
            PrintProgress(i + 1, pages.Count, false);
        }

        var fileName2 =
            $@"{_dir}\HTMLs\{i + 1 - (i % _numberOfChaptersPerFile == 0 ? _numberOfChaptersPerFile : i % _numberOfChaptersPerFile)}-{i}.html";
        await Write(chapters, fileName2);
        PrintProgress(i, pages.Count);
    }
}