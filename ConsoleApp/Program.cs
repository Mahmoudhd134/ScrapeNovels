using System.Text.Json;
using Application;
using Application.DTOs.Novel;
using Application.Helpers;
using Application.Implementation.NovelWebsites;
using Application.Interfaces;
using Application.MediatR.Novle;
using Infrastructure.Epub;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

// Usage: dotnet run -- [url] [websiteName] [outputDir]
// Each chapter is saved under <outputDir>/<novel name>/ as it is scraped; re-running the same
// command resumes from there. The combined <novel name>.json and <novel name>.epub are written
// once all chapters succeed.
//
// Usage: dotnet run -- <path to novel .json>
// Converts an already-scraped novel JSON (with or without volumes) to an .epub next to it.
var url = args.ElementAtOrDefault(0) ?? "https://cenele.com/cont/kingm-bline/";
var websiteName = args.ElementAtOrDefault(1) ?? "Riwyat";
var dir = args.ElementAtOrDefault(2) ?? @"D:\SideProjects\ScrapeNovels\jsons";

var serviceProvider = new ServiceCollection()
    .AddApplicationConfiguration()
    .AddSingleton<IEpubServices, EpubServices>()
    .BuildServiceProvider();

var mediator = serviceProvider.GetRequiredService<IMediator>();
var epubServices = serviceProvider.GetRequiredService<IEpubServices>();

if (url.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(url))
{
    var epubPath = Path.ChangeExtension(url, ".epub");
    await using (var json = File.OpenRead(url))
    {
        var root = (await JsonDocument.ParseAsync(json)).RootElement;
        if (root.TryGetProperty(nameof(NovelDto.Volumes), out _))
            await epubServices.MakeEpub(root.Deserialize<NovelDto>(), epubPath);
        else
            await epubServices.MakeEpub(root.Deserialize<NovelWithNoVolumesDto>(), epubPath);
    }

    Console.WriteLine($"Saved to {epubPath}");
    return;
}

if (!Enum.TryParse<AllWebsites>(websiteName, true, out var websiteEnum))
    websiteEnum = AllWebsites.NotFound;

var webSite = WebsiteUtilityFunctions.GetNovelWebsite(websiteEnum);

UtilityFunctions.CheckDirectory(dir);

// 69shuba has a flat chapter catalog (no volumes), so it uses the no-volume handler.
if (websiteEnum == AllWebsites.Shuba69)
{
    Console.WriteLine($"Downloading (no volume separators): {url}");

    var flatNovel = await mediator.Send(new GetNovelWithNoChaptersSeparator.Query(webSite, url, dir));

    var flatFileName = UtilityFunctions.MakeValidFileNameFromString(
        string.IsNullOrWhiteSpace(flatNovel.Name) ? "novel" : flatNovel.Name.Trim());
    var flatOutputPath = Path.Combine(dir, flatFileName + ".json");

    await JsonUtilityFunctions.WriteToFile(flatNovel, flatOutputPath);
    await epubServices.MakeEpub(flatNovel, Path.ChangeExtension(flatOutputPath, ".epub"));

    var flatEmptyBodies = flatNovel.Chapters.Count(c => c.Body is null || c.Body.Count == 0);
    Console.WriteLine($"\nDone! Novel \"{flatNovel.Name}\" — {flatNovel.Chapters.Count} chapter(s).");
    Console.WriteLine($"Chapters with empty body: {flatEmptyBodies}");
    Console.WriteLine($"Saved to {flatOutputPath} (+ .epub)");
    return;
}

Console.WriteLine($"Downloading (with volume separators): {url}");

var novel = await mediator.Send(new GetNovelWithVolumesSeparator.Query(webSite, url, dir));

var fileName = UtilityFunctions.MakeValidFileNameFromString(
    string.IsNullOrWhiteSpace(novel.Name) ? "novel" : novel.Name.Trim());
var outputPath = Path.Combine(dir, fileName + ".json");

await JsonUtilityFunctions.WriteToFile(novel, outputPath);
await epubServices.MakeEpub(novel, Path.ChangeExtension(outputPath, ".epub"));

var chapterCount = novel.Volumes.Sum(v => v.Chapters.Count);
var emptyBodies = novel.Volumes.SelectMany(v => v.Chapters).Count(c => c.Body is null || c.Body.Count == 0);
Console.WriteLine($"\nDone! Novel \"{novel.Name}\" — {novel.Volumes.Count} volume(s), {chapterCount} chapter(s).");
Console.WriteLine($"Chapters with empty body: {emptyBodies}");
Console.WriteLine($"Saved to {outputPath} (+ .epub)");
