namespace Application.DTOs;

public class VolumeDto
{
    public string Title { get; set; }
    public IEnumerable<ChapterDto> Chapters { get; set; }
}