namespace Domain.Abstractions;

public abstract class Scraper
{
    protected readonly IWebSite WebSite;

    protected Scraper() => throw new ArgumentException("You must specified an object implements IWebsite interface");

    protected Scraper(IWebSite webSite)
    {
        WebSite = webSite;
    }

    public abstract Task StartWithNoVolumesSeparators(int numberOfChaptersPerFile);

    public abstract Task StartWithVolumesSeparators();
}