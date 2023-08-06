using Application.DTOs;

namespace Application.Abstractions;

public interface IPdfMaker
{
    public Task MakeFromChapters(IEnumerable<ChapterDto> content, string outputPath, int whiteLinesBetweenLines = 1,
        string fontSize = "15px");
}