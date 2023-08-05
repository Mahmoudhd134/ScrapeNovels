namespace Domain.Models;

public class VolumeLinkInfo
{
    public string Title { get; set; }
    public IEnumerable<ChapterLinkInfo> Chapters { get; set; }
}