namespace Application.DTOs;

public class NovelDto
{
    public string Name { get; set; }
    public IList<VolumeDto> Volumes { get; set; }
}