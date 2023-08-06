namespace Application.DTOs;

public class NovelWithNoVolumesDto
{
    public string Name { get; set; }
    public IList<ChapterDto> Chapters { get; set; }
}