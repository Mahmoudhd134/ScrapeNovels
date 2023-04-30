Console.WriteLine("Enter the directory to download in...");
Console.WriteLine("To use this directory (which the program is in) type .");
Console.Write(">> ");
var dir = Console.ReadLine();

Console.Write("Enter the url of the main page of the novel in kol novel website >> ");
var url = Console.ReadLine();

Console.Write(
    "Enter the font size (for mobile try 3em or 4em or 5em and so on, for printing A4 try 1em or 2em and so on) >> ");
var fontSize = Console.ReadLine();

Console.Write("Enter number of chapters per file (for best result make it between below 250) >> ");
var numberOfChaptersPerFile = int.Parse(Console.ReadLine() ?? "");

Console.Write("Enter the number of white spaces between each line (1,2,3...) >> ");
var whiteLinesBetweenLines = int.Parse(Console.ReadLine() ?? "");

// var dir = @"C:\Users\nasse\OneDrive\Desktop\KolNovel\ttt";
// var dir = @"./ff jj/aa f";
// var url = "https://kolnovel.com/series/the-kings-avatar-for-the-glory/";
// var fontSize = "3em";
// var whiteLinesBetweenLines = 1;
// var numberOfChaptersPerFile = 7;
try
{
    var scrapper = new AutomateScrape(url, dir, fontSize, whiteLinesBetweenLines, numberOfChaptersPerFile);
    await scrapper.Start();
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