using System.Diagnostics;
using Domain.Models;

namespace Application.Helpers;

public static class UtilityFunctions
{
    public static async Task<string> GetHtmlFromUrl(string url)
    {
        HttpClient httpClient = new();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
        try
        {
            return await httpClient.GetStringAsync(url);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public static string MakeValidFileNameFromString(string filename) =>
        Path.GetInvalidFileNameChars().Aggregate(filename, (current, c) => current.Replace(c, '@'));

    public static void PrintProgress(int i, int count, bool lineBefore = true, bool newLine = true) =>
        Console.Write($"\r{(lineBefore ? "\n" : "")}Chapter ({i}/{count}) Done!!" + (newLine ? "\n" : ""));

    public static async Task Write(IEnumerable<Chapter> chapters, string fileName,
        int whiteLinesBetweenLines, string fontSize, string outputDir)
    {
        CheckDirectory(Path.GetDirectoryName(fileName));
        var refactored = chapters
            .Select(chs => string.Join(Repeat("<br/>", whiteLinesBetweenLines - 1),
                chs.Body.Select(l => $"<p>{l}</p>").Prepend(chs.Title + "<br/><br/>")));

        var html = "<!DOCTYPE html>" +
                   "<html><head>" +
                   "<meta charset=\"UTF-8\">" +
                   "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">" +
                   "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">" +
                   "<title>Document</title>" +
                   $"</head><body dir=\"rtl\" style=\"font-size:{fontSize};\">" +
                   string.Join("<p>" + Repeat("-", 200) + "</p>", refactored) +
                   "</body></html>";

        await File.WriteAllTextAsync(fileName, html);

        var outputFileFullPath = $"{outputDir}\\{Path.GetFileNameWithoutExtension(fileName)}.pdf";
        ConvertToPdf(fileName, outputFileFullPath);
    }

    public static string Repeat(string s, int count) => string.Concat(Enumerable.Repeat(s, count));

    public static void CheckDirectory(string dir)
    {
        if (Directory.Exists(dir) == false)
            Directory.CreateDirectory(dir);
    }

    private static void ConvertToPdf(string htmlPath, string outputPdfPath)
    {
        CheckDirectory(Path.GetDirectoryName(outputPdfPath));
        for (var i = 0; i < 3; i++)
        {
            var process = new Process();
            process.StartInfo.FileName = "wkhtmltopdf.exe";
            process.StartInfo.Arguments = $"\"{htmlPath}\" \"{outputPdfPath}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;

            Console.WriteLine("\nStart Converting...");

            process.Start();
            process.WaitForExit();

            if (process.ExitCode == 0)
                break;

            Console.WriteLine(
                $"\n\nConversion failed with exit code {process.ExitCode}\nfrom <<{htmlPath}>> to <<{outputPdfPath}>>");
            Console.WriteLine($"trying number {i + 1} failed");
            if (i == 2)
            {
                throw new Exception(
                    $"\nConversion failed with exit code {process.ExitCode}\nfrom <<{htmlPath}>> to <<{outputPdfPath}>>");
            }

            Console.WriteLine("Retrying...");
            Console.WriteLine();
        }
    }
}