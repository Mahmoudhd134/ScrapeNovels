namespace Application.DTOs.Novel;

public class ChapterDto
{
    public string Title { get; set; }
    public IList<string> Body { get; set; }
}