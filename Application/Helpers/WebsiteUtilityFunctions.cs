using Application.WebSites;
using Domain.Abstractions;

namespace Application.Helpers;

public class WebsiteUtilityFunctions
{
    private static readonly Dictionary<string, Type> Websites = new()
    {
        { "KolNovel", typeof(KolNovel) },
        { "Riwyat", typeof(Riwyat) }
    };

    public static WebSite GetWebsite(string name, params object[] args)
    {
        Type type;
        try
        {
            type = Websites[name];
        }
        catch (KeyNotFoundException e)
        {
            throw new KeyNotFoundException(
                $"The website '{name}' does not exist, The available websites are {GetAllWebsites().GetFormattedString()}");
        }

        return (WebSite)Activator.CreateInstance(type, args);
    }

    public static ICollection<string> GetAllWebsites()
    {
        return Websites.Keys;
    }
}