using Application.Abstractions;
using Application.Implementation.WebSites;

namespace Application.Helpers;

public class WebsitesUtilityFunctions
{
    public static readonly IEnumerable<string> AllWebsitesNames =
        Enum.GetValues<AllWebsites>().SkipLast(1).Select(x => x.ToString());

    public static WebSite GetWebSite(AllWebsites name, string url)
    {
        var ex = new AggregateException(
            $"Can not find the website, The available websites are {AllWebsitesNames.GetFormattedString()}");
        Func<WebSite> Get = name switch
        {
            AllWebsites.KolNovel => () => new KolNovel(url),
            AllWebsites.Riwyat => () => new Riwyat(url),
            AllWebsites.NotFound => throw ex,
            _ => throw ex
        };
        return Get();
    }
}