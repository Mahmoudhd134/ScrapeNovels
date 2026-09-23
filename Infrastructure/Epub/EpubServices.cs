using Application.DTOs.Novel;
using Application.Interfaces;
using EpubCore.Fluent;

namespace Infrastructure.Epub;

public class EpubServices : IEpubServices
{
    public async Task MakeEpub(NovelDto novel, string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (Directory.Exists(dir) == false)
        {
            Directory.CreateDirectory(dir);
        }

        var builder = EpubBookBuilder.Create();

        builder
            .WithTitle(novel.Name)
            .WithUniqueIdentifier("F7311F63-879E-4EE8-B7BA-168B207F4752")
            .AddAuthor("No Author")
            .AddChapter("الاول", "هذا هو محتوي الاول")
            .Build(outputPath);
        return;

        foreach (var chapterDto in novel.Volumes.SelectMany(x => x.Chapters))
        {
            var html = $"<h3>{chapterDto.Title}</h3>" +
                       string.Join("\n", chapterDto.Body.Select(x => $"<p>{x}</p>"));
            builder.AddChapter(chapterDto.Title, html);
        }

        builder.Build(outputPath);
    }
}