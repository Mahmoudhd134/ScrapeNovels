using Domain.Models;

namespace Domain.Abstractions
{
    public interface IWebSite
    {
        public Task<IList<ChapterLinkInfo>> GetAllPages(string url);
        public Task<IList<VolumeLinkInfo>> GetVolumePages(string url);
        public Task<Chapter> GetChapter(string url);
    }
}