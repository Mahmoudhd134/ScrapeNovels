using logic.Automating;
using logic.Automating.Scrapers;

// Console.WriteLine("Enter the directory to download in...");
// Console.WriteLine("To use this directory (which the program is in) type .");
// Console.Write(">> ");
// var dir = Console.ReadLine();
//
// Console.Write("Enter the url of the main page of the novel in kol novel website >> ");
// var url = Console.ReadLine();
//
// Console.Write(
//     "Enter the font size (for mobile try 3em or 4em or 5em and so on, for printing A4 try 1em or 2em and so on) >> ");
// var fontSize = Console.ReadLine();
//
// Console.Write("Enter the number of white spaces between each line (1,2,3...) >> ");
// var whiteLinesBetweenLines = int.Parse(Console.ReadLine() ?? "");
//
// Console.WriteLine("Kolnovel => 1, Riwyat => 2 >> ........ ");
// var type = int.Parse(Console.ReadLine() ?? "");
var dir = @"C:\Users\nasse\OneDrive\Desktop\KolNovel\tttttttttttttttggg";
// var dir = @"./ff jj/aa f";
var url = "https://kolnovel.com/series/revenge/";
url =
    "https://kolnovel.com/series/00%d8%a7%d9%84%d8%a8%d8%af%d8%a7%d9%8a%d8%a9-%d8%a8%d8%b9%d8%af-%d8%a7%d9%84%d9%86%d9%87%d8%a7%d9%8a%d8%a9/";
var fontSize = "4rem";
var whiteLinesBetweenLines = 1;
var type = 1;
try
{
    AutomateScrape scrapper =
        type switch
        {
            1 => new KolNovelAutomateScrape(url, dir, fontSize, whiteLinesBetweenLines),
            2 => new RiwyatAutomateScrape(url, dir, fontSize, whiteLinesBetweenLines),
            _ => throw new ArgumentException("Must be 1 or 2")
        };
    Console.Write(
        @"Do you want to separate the novel with volumes or custom number of chapter per file (we will get the number later)...
 true for entering the number of chapters per file or false for yes to separate with volumes?");
    bool withNoVolumesSeparators;
    try
    {
        withNoVolumesSeparators = bool.Parse(Console.ReadLine() ?? "false");
    }
    catch (Exception e)
    {
        withNoVolumesSeparators = false;
    }

    if (withNoVolumesSeparators)
    {
        Console.Write("Number of chapters per file >> ");
        await scrapper.StartWithNoVolumesSeparators(int.Parse(Console.ReadLine() ?? "300"));
    }
    else
        await scrapper.StartWithVolumesSeparators();
}
catch (Exception e)
{
    Console.WriteLine(e.Message);
    Console.WriteLine(e.InnerException?.Message);
    Console.WriteLine(e);
    Console.ReadLine();
    throw;
}


Console.WriteLine("Press any key to exit!");
Console.ReadLine();