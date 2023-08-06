namespace Domain.NovelModels;

public class Volume
{
    public string Title { get; set; }
    public IList<Chapter> Chapters { get; set; }
}