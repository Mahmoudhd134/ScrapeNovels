using Application.DTOs.Novel;

namespace Application.Interfaces;

public interface IEpubServices
{
    // Volumes become nested entries in the table of contents, each with its own title page.
    Task MakeEpub(NovelDto novel, string outputPath);

    // Chapters are listed flat in the table of contents.
    Task MakeEpub(NovelWithNoVolumesDto novel, string outputPath);
}
