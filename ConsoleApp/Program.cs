using Application.Helpers;
using Application.Scrapers;
using Domain.Abstractions;
using Infrastructure;

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
// Console.WriteLine($"Choose an website\n{WebsiteUtilityFunctions.GetAllWebsites().GetFormattedString()}");
// var websiteName = Console.ReadLine();

var dir = @"C:\Users\nasse\OneDrive\Desktop\KolNovel\test";
// var dir = @"./ff jj/aa f";
var url = "https://kolnovel.com/series/what-do-you-do-at-the-end-of-the-world//";
var fontSize = "3.5rem";
var whiteLinesBetweenLines = 1;
var websiteName = "KolNovel";


WebSite site;
try
{
    site = WebsiteUtilityFunctions.GetWebsite(websiteName, url);
}
catch (Exception e)
{
    Console.WriteLine(e.Message);
    throw;
}

var scrapper = new ToPdfScraper(site, new MakePdfWithWkhtmltopdf(whiteLinesBetweenLines, fontSize), dir);


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
    await scrapper.StartWithNoVolumesSeparators(int.Parse(Console.ReadLine() ?? "100"));
}
else
{
    await scrapper.StartWithVolumesSeparators();
}

Console.WriteLine("Press any key to exit!");
Console.ReadLine();