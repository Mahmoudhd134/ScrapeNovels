namespace Domain.Models;

public class Chapter
{
    public string Title { get; set; }
    public IEnumerable<string> Body { get; set; }
}