namespace ConsoleApp.WebSites
{
    public abstract class WebSite<T>
    {
        public readonly string Url;

        public WebSite(string url)
        {
            Url = url;
        }

        public virtual async Task<string> GetHtml()
        {
            HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
            try
            {
                return await httpClient.GetStringAsync(Url);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public abstract Task<T> Parse(string html);
    }
}