namespace Application.DTOs.Novel;

public class VolumeDto
{
    public string Title { get; set; }
    public IList<ChapterDto> Chapters { get; set; }
}