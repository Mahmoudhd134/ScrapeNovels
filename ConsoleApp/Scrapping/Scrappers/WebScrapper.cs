using ConsoleApp.WebSites;

namespace ConsoleApp.Scrapping.Scrappers
{
    public class WebScrapper<T> : IScrapper<T>
    {
        private readonly WebSite<T> _webSite;

        public WebScrapper(WebSite<T> webSite) => _webSite = webSite;

        public string GetUrl() => _webSite.Url;

        public async Task<T> GetData() => await _webSite.Parse(await _webSite.GetHtml());
    }
}