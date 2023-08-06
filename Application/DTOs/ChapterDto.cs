namespace Application.DTOs;

public class ChapterDto
{
    public string Title { get; set; }
    public IEnumerable<string> Body { get; set; }
}