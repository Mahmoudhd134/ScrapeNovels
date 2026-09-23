using Application.DTOs.Novel;

namespace Application.Interfaces;

public interface IEpubServices
{
    Task MakeEpub(NovelDto novel, string outputPath);
}