using Application;
using Application.Abstractions;
using Application.Helpers;
using Application.Implementation.WebSites;
using Application.MediatR.NovelToPdf;
using Infrastructure.Pdf;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

// Console.WriteLine("Enter the directory to download in...");
// Console.WriteLine("To use this directory (which the program is in) type .");
// Console.Write(">> ");
// var dir = Console.ReadLine();
//
// Console.Write("Enter the url of the main page of the novel in the website (which contains the all chapters) >> ");
// var url = Console.ReadLine();
//
// Console.Write(
//     "Enter the font size (for mobile try 3em or 4em or 5em and so on, for printing A4 try 1em or 2em and so on) >> ");
// var fontSize = Console.ReadLine();
//
// Console.Write("Enter the number of white spaces between each line (1,2,3...) >> ");
// var whiteLinesBetweenLines = int.Parse(Console.ReadLine() ?? "");
//
// Console.WriteLine($"Choose an website\n{WebsitesUtilityFunctions.AllWebsitesNames.GetFormattedString()}");
// var websiteName = Console.ReadLine();

var dir = @"C:\Users\nasse\OneDrive\Desktop\KolNovel\test";
// var dir = @"./ff jj/aa f";
var url = "https://kolnovel.com/series/what-do-you-do-at-the-end-of-the-world//";
var fontSize = "3.5rem";
var whiteLinesBetweenLines = 1;
var websiteName = "KolNovel";

var serviceProvider = new ServiceCollection()
    .AddApplicationConfiguration()
    .AddScoped<IPdfMaker, MakePdfWithWkhtmltopdf>()
    .BuildServiceProvider();

var mediatR = serviceProvider.GetRequiredService<IMediator>();

var success = Enum.TryParse<AllWebsites>(websiteName, true, out var websiteEnum);
if (!success)
    websiteEnum = AllWebsites.NotFound;

var webSite = WebsitesUtilityFunctions.GetWebSite(websiteEnum, url);

Console.Write(
    @"Do you want to separate the novel with volumes or custom number of chapter per file (we will get the number later)...
true for entering the number of chapters per file or false for yes to separate with volumes.... >> ");
bool withNoVolumesSeparators;
try
{
    withNoVolumesSeparators = bool.Parse(Console.ReadLine() ?? "");
}
catch (Exception e)
{
    withNoVolumesSeparators = false;
}


if (withNoVolumesSeparators)
{
    Console.Write("Number of chapters per file >> ");
    await mediatR.Send(new MakePdfByCustomSeparator.Query(webSite, dir, fontSize, whiteLinesBetweenLines,
        int.Parse(Console.ReadLine() ?? "200")));
}
else
{
    await mediatR.Send(new MakePdfByVolumeSeparators.Query(webSite, dir, fontSize, whiteLinesBetweenLines));
}

Console.WriteLine("Press any key to exit!");
Console.ReadLine();