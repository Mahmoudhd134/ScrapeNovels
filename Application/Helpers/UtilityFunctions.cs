namespace Application.Helpers;

public static class UtilityFunctions
{
    private static readonly Random Random = new();

    // Fetches a page with a rotating (realistic) User-Agent and retries transient failures a few
    // times before giving up. Rotating the UA per request and backing off helps get past light
    // rate-limiting / bot filtering on large novels (hundreds of requests).
    public static async Task<string> GetHtmlFromUrl(string url, int maxRetries = 45, bool showErrorLog = true)
    {
        Exception lastError = null;

        for (var retry = 0; retry < maxRetries; retry++)
        {
            using var httpClient = new HttpClient();

            bool success;
            do
            {
                var userAgent = Data.UserAgents[Random.Next(Data.UserAgents.Count)];
                success = httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd(userAgent);
            } while (success == false);

            try
            {
                return await httpClient.GetStringAsync(url);
            }
            catch (Exception e)
            {
                lastError = e;

                // Only start logging once we're well into the retries, to avoid noise.
                if (showErrorLog && retry > 2 * maxRetries / 3)
                    Console.WriteLine($"Failed >> {url}, retry {retry + 1}/{maxRetries}, Message {e.Message}");

                await Task.Delay(300);
            }
        }

        throw lastError ?? new Exception($"Can not get the url even after {maxRetries} retries!!! ({url})");
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