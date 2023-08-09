using Application.Implementation.NovelWebsites;
using Domain.Websites;

namespace Application.Helpers;

public class WebsiteUtilityFunctions
{
    public static readonly IEnumerable<string> AllWebsitesNames =
        Enum.GetValues<AllWebsites>().SkipLast(1).Select(x => x.ToString());

    public static NovelWebsite GetNovelWebsite(AllWebsites name)
    {
        var ex = new AggregateException(
            $"Can not find the website, The available websites are {AllWebsitesNames.GetFormattedString()}");
        Func<NovelWebsite> Get = name switch
        {
            AllWebsites.KolNovel => () => new KolNovel(),
            AllWebsites.Riwyat => () => new Riwyat(),
            AllWebsites.NotFound => throw ex,
            _ => throw ex
        };
        return Get();
    }
}