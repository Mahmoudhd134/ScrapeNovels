using System.Diagnostics;
using Application.DTOs;
using Application.DTOs.Novel;
using Application.Helpers;
using Application.Interfaces;

namespace Infrastructure.Pdf;

public class MakePdfWithWkhtmltopdf : IPdfMaker
{
    

    public async Task MakeFromChapters(IEnumerable<ChapterDto> content, string outputPath,int whiteLinesBetweenLines,string fontSize)
    {
        var dirForOutPath = Path.GetDirectoryName(outputPath);
        UtilityFunctions.CheckDirectory(dirForOutPath);

        var html = MakeHtmlContent(content, whiteLinesBetweenLines, fontSize);
        var tempPathForHtmlFile = Path.Combine(dirForOutPath!, Guid.NewGuid() + ".html");
        await File.WriteAllTextAsync(tempPathForHtmlFile, html);

        ConvertToPdf(tempPathForHtmlFile, outputPath);

        File.Delete(tempPathForHtmlFile);
    }

    private static string MakeHtmlContent(IEnumerable<ChapterDto> chapters, int whiteLinesBetweenLines, string fontSize)
    {
        var refactored = chapters
            .Select(chs => string.Join(UtilityFunctions.Repeat("<br/>", whiteLinesBetweenLines - 1),
                chs.Body.Select(l => $"<p>{l}</p>").Prepend(chs.Title + "<br/><br/>")));

        return "<!DOCTYPE html>" +
               "<html><head>" +
               "<meta charset=\"UTF-8\">" +
               "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">" +
               "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">" +
               "<title>Document</title>" +
               $"</head><body dir=\"rtl\" style=\"font-size:{fontSize};\">" +
               string.Join("<p>" + UtilityFunctions.Repeat("-", 200) + "</p>", refactored) +
               "</body></html>";
    }

    private static void ConvertToPdf(string htmlPath, string outputPdfPath)
    {
        UtilityFunctions.CheckDirectory(Path.GetDirectoryName(outputPdfPath));
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
                throw new Exception(
                    $"\nConversion failed with exit code {process.ExitCode}\nfrom <<{htmlPath}>> to <<{outputPdfPath}>>");

            Console.WriteLine("Retrying...");
            Console.WriteLine();
        }
    }
}