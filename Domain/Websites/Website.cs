namespace Domain.Websites;

public abstract class Website
{
    public virtual async Task<string> GetHtml(string url)
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
}