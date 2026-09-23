using Application;
using Application.Helpers;
using Application.Implementation.NovelWebsites;
using Application.MediatR.Novle;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

var url = "https://cenele.com/cont/kingm-bline/";
var dir = @"D:\SideProjects\ScrapeNovels\jsons";
var websiteName = "Riwyat";

var serviceProvider = new ServiceCollection()
    .AddApplicationConfiguration()
    .BuildServiceProvider();

var mediator = serviceProvider.GetRequiredService<IMediator>();

if (!Enum.TryParse<AllWebsites>(websiteName, true, out var websiteEnum))
    websiteEnum = AllWebsites.NotFound;

var webSite = WebsiteUtilityFunctions.GetNovelWebsite(websiteEnum);

Console.WriteLine($"Downloading (with volume separators): {url}");

var novel = await mediator.Send(new GetNovelWithVolumesSeparator.Query(webSite, url));

UtilityFunctions.CheckDirectory(dir);
var fileName = UtilityFunctions.MakeValidFileNameFromString(
    string.IsNullOrWhiteSpace(novel.Name) ? "novel" : novel.Name.Trim());
var outputPath = Path.Combine(dir, fileName + ".json");

await JsonUtilityFunctions.WriteToFile(novel, outputPath);

var chapterCount = novel.Volumes.Sum(v => v.Chapters.Count);
var emptyBodies = novel.Volumes.SelectMany(v => v.Chapters).Count(c => c.Body is null || c.Body.Count == 0);
Console.WriteLine($"\nDone! Novel \"{novel.Name}\" — {novel.Volumes.Count} volume(s), {chapterCount} chapter(s).");
Console.WriteLine($"Chapters with empty body: {emptyBodies}");
Console.WriteLine($"Saved to {outputPath}");
