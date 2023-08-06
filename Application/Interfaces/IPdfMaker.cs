using Application.DTOs;

namespace Application.Interfaces;

public interface IPdfMaker
{
    public Task MakeFromChapters(IEnumerable<ChapterDto> content, string outputPath);
}