namespace Domain.NovelModels;

public class Novel
{
    public string NovelName { get; set; }
    public IList<Volume> Volumes { get; set; }
}