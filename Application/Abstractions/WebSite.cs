using Domain.NovelModels;

namespace Application.Abstractions;

public abstract class WebSite
{
    protected WebSite(string baseUrl)
    {
        BaseUrl = baseUrl;
    }

    protected string BaseUrl { get; set; }

    public abstract Task<IList<ChapterLinkInfo>> GetAllPages();
    public abstract Task<IList<VolumeLinkInfo>> GetVolumePages();
    public abstract Task<string> GetNovelName();
    public abstract Task<Chapter> GetChapter(string url);
}