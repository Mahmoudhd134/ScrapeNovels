using Application.DTOs;
using Application.DTOs.Novel;
using Application.Implementation;
using Application.Implementation.NovelWebsites;
using Domain.NovelModels;
using Domain.Websites;
using MediatR;

namespace Application.MediatR.Novle;

public class GetNovelWithVolumesSeparator
{
    public record Query(NovelWebsite Website, string BaseUrl) : IRequest<NovelDto>;

    public class Handler : IRequestHandler<Query, NovelDto>
    {
        public async Task<NovelDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var volumeLinkInfos = await request.Website.GetVolumePages(request.BaseUrl);
            var novelName = await request.Website.GetNovelName(request.BaseUrl);

            var volumes = new List<VolumeDto>();

            // Process volumes one by one (synchronously)
            for (var i = 0; i < volumeLinkInfos.Count; i++)
            {
                var volume = volumeLinkInfos[i];
                Console.WriteLine($">> Start Volume {i + 1}");

                // Process chapters within this volume in parallel
                var chaptersTasks = volume.Chapters.Select<ChapterLinkInfo, Func<Task<ChapterDto>>>((ch, j) =>
                    async () =>
                    {
                        Console.WriteLine($">> Start Chapter {j + 1} In Volume {i + 1}");
                        var chapter = await request.Website.GetChapter(ch.Url);
                        Console.WriteLine($">> End Chapter {j + 1} In Volume {i + 1}");
                        return new ChapterDto()
                        {
                            Title = $"{ch.Info}   {chapter.Title}",
                            Body = chapter.Body
                        };
                    });

                var chapters = await BatchProcessor.ProcessInBatches(
                    chaptersTasks,
                    batchSize: 100,
                    cancellationToken: cancellationToken);

                Console.WriteLine($">> End Volume {i + 1}");

                volumes.Add(new VolumeDto()
                {
                    Title = volume.Title,
                    Chapters = chapters
                });
            }

            return new NovelDto
            {
                Name = novelName,
                Volumes = volumes.ToArray()
            };
        }
    }
}