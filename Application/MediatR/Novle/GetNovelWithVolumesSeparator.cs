using Application.DTOs;
using Application.DTOs.Novel;
using Application.Helpers;
using Application.Implementation;
using Application.Implementation.NovelWebsites;
using Domain.NovelModels;
using Domain.Websites;
using MediatR;

namespace Application.MediatR.Novle;

public class GetNovelWithVolumesSeparator
{
    // CacheDir: when set, every chapter is saved under <CacheDir>/<novel name>/<NN> - <volume>/
    // as it is scraped, and chapters already there are loaded instead of re-fetched (resume).
    public record Query(NovelWebsite Website, string BaseUrl, string CacheDir = null) : IRequest<NovelDto>;

    public class Handler : IRequestHandler<Query, NovelDto>
    {
        public async Task<NovelDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var volumeLinkInfos = await request.Website.GetVolumePages(request.BaseUrl);
            var novelName = await request.Website.GetNovelName(request.BaseUrl);

            var novelCache = request.CacheDir == null ? null : ChapterCache.ForNovel(request.CacheDir, novelName);
            var failed = new List<string>();

            var volumes = new List<VolumeDto>();

            // Process volumes one by one (synchronously)
            for (var i = 0; i < volumeLinkInfos.Count; i++)
            {
                var volume = volumeLinkInfos[i];
                Console.WriteLine($">> Start Volume {i + 1}");

                var cache = novelCache?.ForVolume(i + 1, volume.Title);
                if (cache != null)
                    Console.WriteLine($">> Resume: {cache.SavedCount}/{volume.Chapters.Count} chapters already saved in Volume {i + 1}");

                // Process chapters within this volume in parallel
                var chaptersTasks = volume.Chapters.Select<ChapterLinkInfo, Func<Task<ChapterDto>>>((ch, j) =>
                    async () =>
                    {
                        var cached = cache == null ? null : await cache.TryLoad(ch.Url);
                        if (cached != null)
                            return cached;

                        Console.WriteLine($">> Start Chapter {j + 1} In Volume {i + 1}");
                        try
                        {
                            var chapter = await request.Website.GetChapter(ch.Url);
                            var dto = new ChapterDto()
                            {
                                // Some sites' page title is the catalog title itself; don't repeat it.
                                Title = chapter.Title == ch.Info ? ch.Info : $"{ch.Info}   {chapter.Title}",
                                Body = chapter.Body
                            };
                            if (cache != null)
                                await cache.Save(j + 1, ch.Url, dto);
                            Console.WriteLine($">> End Chapter {j + 1} In Volume {i + 1}");
                            return dto;
                        }
                        catch (Exception e)
                        {
                            // Don't let one chapter kill the whole run; the rest still get saved.
                            Console.WriteLine($">> Failed Chapter {j + 1} In Volume {i + 1} ({ch.Url}): {e.Message}");
                            lock (failed) failed.Add(ch.Url);
                            return null;
                        }
                    });

                var chapters = await BatchProcessor.ProcessInBatches(
                    chaptersTasks,
                    batchSize: 100,
                    cancellationToken: cancellationToken);

                Console.WriteLine($">> End Volume {i + 1}");

                volumes.Add(new VolumeDto()
                {
                    Title = volume.Title,
                    Chapters = chapters.Where(c => c != null).ToList()
                });
            }

            if (failed.Count > 0)
                throw new Exception(
                    $"{failed.Count} chapter(s) failed. " +
                    (novelCache != null ? "Run again to resume -- saved chapters are skipped." : string.Join(", ", failed)));

            return new NovelDto
            {
                Name = novelName,
                Volumes = volumes.ToArray()
            };
        }
    }
}
