using Application;
using Application.Helpers;
using Application.Implementation.NovelWebsites;
using Application.MediatR.Novle;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

// Usage: dotnet run -- [url] [websiteName] [outputDir]
var url = args.ElementAtOrDefault(0) ?? "https://cenele.com/cont/kingm-bline/";
var websiteName = args.ElementAtOrDefault(1) ?? "Riwyat";
var dir = args.ElementAtOrDefault(2) ?? @"D:\SideProjects\ScrapeNovels\jsons";

var serviceProvider = new ServiceCollection()
    .AddApplicationConfiguration()
    .BuildServiceProvider();

var mediator = serviceProvider.GetRequiredService<IMediator>();

if (!Enum.TryParse<AllWebsites>(websiteName, true, out var websiteEnum))
    websiteEnum = AllWebsites.NotFound;

var webSite = WebsiteUtilityFunctions.GetNovelWebsite(websiteEnum);

UtilityFunctions.CheckDirectory(dir);

// 69shuba has a flat chapter catalog (no volumes), so it uses the no-volume handler.
if (websiteEnum == AllWebsites.Shuba69)
{
    Console.WriteLine($"Downloading (no volume separators): {url}");

    var flatNovel = await mediator.Send(new GetNovelWithNoChaptersSeparator.Query(webSite, url));

    var flatFileName = UtilityFunctions.MakeValidFileNameFromString(
        string.IsNullOrWhiteSpace(flatNovel.Name) ? "novel" : flatNovel.Name.Trim());
    var flatOutputPath = Path.Combine(dir, flatFileName + ".json");

    await JsonUtilityFunctions.WriteToFile(flatNovel, flatOutputPath);

    var flatEmptyBodies = flatNovel.Chapters.Count(c => c.Body is null || c.Body.Count == 0);
    Console.WriteLine($"\nDone! Novel \"{flatNovel.Name}\" — {flatNovel.Chapters.Count} chapter(s).");
    Console.WriteLine($"Chapters with empty body: {flatEmptyBodies}");
    Console.WriteLine($"Saved to {flatOutputPath}");
    return;
}

Console.WriteLine($"Downloading (with volume separators): {url}");

var novel = await mediator.Send(new GetNovelWithVolumesSeparator.Query(webSite, url));

var fileName = UtilityFunctions.MakeValidFileNameFromString(
    string.IsNullOrWhiteSpace(novel.Name) ? "novel" : novel.Name.Trim());
var outputPath = Path.Combine(dir, fileName + ".json");

await JsonUtilityFunctions.WriteToFile(novel, outputPath);

var chapterCount = novel.Volumes.Sum(v => v.Chapters.Count);
var emptyBodies = novel.Volumes.SelectMany(v => v.Chapters).Count(c => c.Body is null || c.Body.Count == 0);
Console.WriteLine($"\nDone! Novel \"{novel.Name}\" — {novel.Volumes.Count} volume(s), {chapterCount} chapter(s).");
Console.WriteLine($"Chapters with empty body: {emptyBodies}");
Console.WriteLine($"Saved to {outputPath}");
