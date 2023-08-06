namespace Domain.NovelModels;

public class Chapter
{
    public string Title { get; set; }
    public IList<string> Body { get; set; }
}