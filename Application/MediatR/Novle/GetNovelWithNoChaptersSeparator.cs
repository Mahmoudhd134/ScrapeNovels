using Application.DTOs.Novel;
using Application.Implementation;
using Application.Implementation.NovelWebsites;
using Domain.NovelModels;
using Domain.Websites;
using MediatR;

namespace Application.MediatR.Novle;

public class GetNovelWithNoChaptersSeparator
{
    public record Query(NovelWebsite Website, string BaseUrl) : IRequest<NovelWithNoVolumesDto>;

    public class Handler : IRequestHandler<Query, NovelWithNoVolumesDto>
    {
        public async Task<NovelWithNoVolumesDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var pages = await request.Website.GetAllPages(request.BaseUrl);
            var novelName = await request.Website.GetNovelName(request.BaseUrl);

            // Create tasks for processing chapters in parallel using batch processor
            var chapterTasks = pages.Select<ChapterLinkInfo, Func<Task<ChapterDto>>>((p, i) =>
                async () =>
                {
                    Console.WriteLine($">> Start Chapter {i + 1}");
                    var chapter = await request.Website.GetChapter(p.Url);
                    Console.WriteLine($">> End Chapter {i + 1}");
                    return new ChapterDto()
                    {
                        Title = $"{p.Info}    {chapter.Title}",
                        Body = chapter.Body
                    };
                });

            var chapters = await BatchProcessor.ProcessInBatches(
                chapterTasks,
                batchSize: 100,
                cancellationToken: cancellationToken);

            return new NovelWithNoVolumesDto()
            {
                Name = novelName,
                Chapters = chapters.Where(c => c != null).ToList()
            };
        }
    }
}