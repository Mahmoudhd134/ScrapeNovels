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

    public static string MakeValidFileNameFromString(string filename)
    {
        return Path.GetInvalidFileNameChars().Aggregate(filename, (current, c) => current.Replace(c, '@'));
    }

    public static void PrintProgress(int i, int count, bool lineBefore = true, bool newLine = true)
    {
        Console.Write($"\r{(lineBefore ? "\n" : "")}Chapter ({i}/{count}) Done!!" + (newLine ? "\n" : ""));
    }


    public static string Repeat(string s, int count)
    {
        return string.Concat(Enumerable.Repeat(s, count));
    }

    public static void CheckDirectory(string dir)
    {
        if (Directory.Exists(dir) == false)
            Directory.CreateDirectory(dir);
    }
}