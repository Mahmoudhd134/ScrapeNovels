using System.Diagnostics;
using logic.Automating;
using logic.Automating.Scrapers;
using Microsoft.AspNetCore.Mvc;
using MVC.Models;

namespace MVC.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> GetKolNovel(string url, string fontSize, string dir, int whiteLinesBetweenLines,
        int numberOfChaptersPerFile)
    {
        AutomateScrape kolNovel = new KolNovelAutomateScrape(url, dir, fontSize, whiteLinesBetweenLines, numberOfChaptersPerFile);
        await kolNovel.Start();
        ViewBag.url = url;
        ViewBag.fontSize = fontSize;
        ViewBag.dir = dir;
        ViewBag.whiteLinesBetweenLines = whiteLinesBetweenLines;
        ViewBag.numberOfChaptersPerFile = numberOfChaptersPerFile;
        return View("Index");
    }
    
    public async Task<IActionResult> GetRiwyat(string url, string fontSize, string dir, int whiteLinesBetweenLines,
        int numberOfChaptersPerFile)
    {
        AutomateScrape kolNovel = new RiwyatAutomateScrape(url, dir, fontSize, whiteLinesBetweenLines, numberOfChaptersPerFile);
        await kolNovel.Start();
        ViewBag.url = url;
        ViewBag.fontSize = fontSize;
        ViewBag.dir = dir;
        ViewBag.whiteLinesBetweenLines = whiteLinesBetweenLines;
        ViewBag.numberOfChaptersPerFile = numberOfChaptersPerFile;
        return View("Index");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}