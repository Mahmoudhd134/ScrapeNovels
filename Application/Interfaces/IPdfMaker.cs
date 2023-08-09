using Application.DTOs;
using Application.DTOs.Novel;

namespace Application.Interfaces;

public interface IPdfMaker
{
    public Task MakeFromChapters(IEnumerable<ChapterDto> content, string outputPath, int whiteLinesBetweenLines = 1,
        string fontSize = "15px");
}