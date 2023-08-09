using Domain.NovelModels;

namespace Domain.Websites;

public abstract class NovelWebsite : Website
{
    public abstract Task<IList<ChapterLinkInfo>> GetAllPages(string url);
    public abstract Task<IList<VolumeLinkInfo>> GetVolumePages(string url);
    public abstract Task<string> GetNovelName(string url);
    public abstract Task<Chapter> GetChapter(string url);
}