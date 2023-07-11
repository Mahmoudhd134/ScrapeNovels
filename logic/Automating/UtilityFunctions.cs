using System.Diagnostics;

namespace logic.Automating;

public static class UtilityFunctions
{

    public static void PrintProgress(int i, int count, bool newLine = true) =>
        Console.Write($"\rChapter ({i}/{count}) Done!!" + (newLine ? "\n" : ""));
    public static async Task Write(IEnumerable<IEnumerable<string>> chapters, string fileName, int whiteLinesBetweenLines, string fontSize, string dir)
    { 
        CheckDirectory(Path.GetDirectoryName(fileName));
        var refactored = chapters
            .Select(c => string.Join(Repeat("<br/>", whiteLinesBetweenLines - 1),
                c.Select(l => $"<p>{l}</p>")));
        var s = "<!DOCTYPE html>" +
                "<html><head>" +
                "<meta charset=\"UTF-8\">" +
                "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">" +
                "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">" +
                "<title>Document</title>" +
                $"</head><body dir=\"rtl\" style=\"font-size:{fontSize};\">" +
                string.Join("<p>" + Repeat("-", 200) + "</p>", refactored) +
                "</body></html>";

        await File.WriteAllTextAsync(fileName, s);

        var outputFileName = $"{dir}\\PDFs\\{Path.GetFileNameWithoutExtension(fileName)}.pdf";
        ConvertToPdf(fileName, outputFileName);
        // File.Delete(fileName);
    }
    public static string Repeat(string s, int count) => string.Concat(Enumerable.Repeat(s, count));
    public static void CheckDirectory(string dir)
    {
        if (Directory.Exists(dir) == false)
            Directory.CreateDirectory(dir);
    }

    private static void ConvertToPdf(string inputName, string outputName)
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
}