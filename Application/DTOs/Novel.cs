namespace Application.DTOs;

public class Novel
{
    public string Name { get; set; }
    public IList<VolumeDto> Volumes { get; set; } = new List<VolumeDto>();
}