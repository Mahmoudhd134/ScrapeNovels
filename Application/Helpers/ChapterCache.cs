using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.DTOs.Novel;

namespace Application.Helpers;

// Saves every scraped chapter to its own file so a crashed/stopped run can resume.
//
// Layout:
//   <rootDir>/<novel name>/<index>_<id>.json                      (no volumes)
//   <rootDir>/<novel name>/<NN> - <volume title>/<index>_<id>.json (with volumes)
//
// <id> is a short hash of the chapter URL, so it identifies the chapter uniquely even if
// the site inserts/reorders chapters later (the <index> prefix is only for sorting).
public class ChapterCache
{
    private readonly string _dir;

    // chapter id -> file path, for the chapters already saved in this folder.
    private readonly Dictionary<string, string> _existing;

    private ChapterCache(string dir)
    {
        _dir = dir;
        UtilityFunctions.CheckDirectory(_dir);
        _existing = Directory.EnumerateFiles(_dir, "*_*.json")
            .GroupBy(p => Path.GetFileNameWithoutExtension(p).Split('_').Last())
            .ToDictionary(g => g.Key, g => g.First());
    }

    public int SavedCount => _existing.Count;

    public static ChapterCache ForNovel(string rootDir, string novelName) =>
        new(Path.Combine(rootDir, SafeName(novelName, "novel")));

    public ChapterCache ForVolume(int volumeIndex, string volumeTitle) =>
        new(Path.Combine(_dir, $"{volumeIndex:D2} - {SafeName(volumeTitle, "volume")}"));

    public async Task<ChapterDto> TryLoad(string url)
    {
        if (_existing.TryGetValue(Id(url), out var path) == false)
            return null;

        try
        {
            var cached = JsonSerializer.Deserialize<CachedChapter>(await File.ReadAllTextAsync(path));
            return cached?.Body is { Count: > 0 } ? new ChapterDto { Title = cached.Title, Body = cached.Body } : null;
        }
        catch (JsonException)
        {
            // Corrupt/partial file -> treat as missing and re-fetch.
            return null;
        }
    }

    public async Task Save(int index, string url, ChapterDto chapter)
    {
        var path = Path.Combine(_dir, $"{index:D5}_{Id(url)}.json");
        var json = JsonUtilityFunctions.GetJsonString(new CachedChapter
        {
            Url = url,
            Title = chapter.Title,
            Body = chapter.Body
        });

        // Write to a temp file then move, so a crash mid-write never leaves a half file.
        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    private static string Id(string url) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(url)))[..10].ToLowerInvariant();

    private static string SafeName(string name, string fallback) =>
        UtilityFunctions.MakeValidFileNameFromString(string.IsNullOrWhiteSpace(name) ? fallback : name.Trim());

    private class CachedChapter
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public IList<string> Body { get; set; }
    }
}
