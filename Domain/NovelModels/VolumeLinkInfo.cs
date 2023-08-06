namespace Domain.NovelModels;

public class VolumeLinkInfo
{
    public string Title { get; set; }
    public IList<ChapterLinkInfo> Chapters { get; set; }
}