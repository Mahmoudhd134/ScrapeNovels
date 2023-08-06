namespace Domain.Abstractions;

public abstract class Scraper
{
    protected readonly WebSite WebSite;

    protected Scraper()
    {
        throw new ArgumentException("You must specified an object implements IWebsite interface");
    }

    protected Scraper(WebSite webSite)
    {
        WebSite = webSite;
    }

    public abstract Task StartWithNoVolumesSeparators(int numberOfChaptersPerFile);

    public abstract Task StartWithVolumesSeparators();
}