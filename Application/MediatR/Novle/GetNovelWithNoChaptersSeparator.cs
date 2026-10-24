using Application.DTOs.Novel;
using Application.Helpers;
using Application.Implementation;
using Application.Implementation.NovelWebsites;
using Domain.NovelModels;
using Domain.Websites;
using MediatR;

namespace Application.MediatR.Novle;

public class GetNovelWithNoChaptersSeparator
{
    // CacheDir: when set, every chapter is saved under <CacheDir>/<novel name>/ as it is
    // scraped, and chapters already there are loaded instead of re-fetched (resume).
    public record Query(NovelWebsite Website, string BaseUrl, string CacheDir = null) : IRequest<NovelWithNoVolumesDto>;

    public class Handler : IRequestHandler<Query, NovelWithNoVolumesDto>
    {
        public async Task<NovelWithNoVolumesDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var pages = await request.Website.GetAllPages(request.BaseUrl);
            var novelName = await request.Website.GetNovelName(request.BaseUrl);

            var cache = request.CacheDir == null ? null : ChapterCache.ForNovel(request.CacheDir, novelName);
            if (cache != null)
                Console.WriteLine($">> Resume: {cache.SavedCount}/{pages.Count} chapters already saved");

            var failed = new List<string>();

            // Create tasks for processing chapters in parallel using batch processor
            var chapterTasks = pages.Select<ChapterLinkInfo, Func<Task<ChapterDto>>>((p, i) =>
                async () =>
                {
                    var cached = cache == null ? null : await cache.TryLoad(p.Url);
                    if (cached != null)
                        return cached;

                    Console.WriteLine($">> Start Chapter {i + 1}");
                    try
                    {
                        var chapter = await request.Website.GetChapter(p.Url);
                        var dto = new ChapterDto()
                        {
                            // Some sites' page title is the catalog title itself; don't repeat it.
                            Title = chapter.Title == p.Info ? p.Info : $"{p.Info}    {chapter.Title}",
                            Body = chapter.Body
                        };
                        if (cache != null)
                            await cache.Save(i + 1, p.Url, dto);
                        Console.WriteLine($">> End Chapter {i + 1}");
                        return dto;
                    }
                    catch (Exception e)
                    {
                        // Don't let one chapter kill the whole run; the rest still get saved.
                        Console.WriteLine($">> Failed Chapter {i + 1} ({p.Url}): {e.Message}");
                        lock (failed) failed.Add(p.Url);
                        return null;
                    }
                });

            var chapters = await BatchProcessor.ProcessInBatches(
                chapterTasks,
                batchSize: 100,
                cancellationToken: cancellationToken);

            if (failed.Count > 0)
                throw new Exception(
                    $"{failed.Count} chapter(s) failed. " +
                    (cache != null ? "Run again to resume -- saved chapters are skipped." : string.Join(", ", failed)));

            return new NovelWithNoVolumesDto()
            {
                Name = novelName,
                Chapters = chapters.Where(c => c != null).ToList()
            };
        }
    }
}
