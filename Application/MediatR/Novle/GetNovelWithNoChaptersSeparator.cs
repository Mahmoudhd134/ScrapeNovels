using Application.DTOs.Novel;
using Domain.Websites;
using MediatR;

namespace Application.MediatR.Novle;

public class GetNovelWithNoChaptersSeparator
{
    public record Query(NovelWebsite Website,string BaseUrl) : IRequest<NovelWithNoVolumesDto>;

    public class Handler : IRequestHandler<Query, NovelWithNoVolumesDto>
    {
        public async Task<NovelWithNoVolumesDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var pages = await request.Website.GetAllPages(request.BaseUrl);
            return new NovelWithNoVolumesDto()
            {
                Name = await request.Website.GetNovelName(request.BaseUrl),
                Chapters = pages.Select(async p =>
                    {
                        var chapter = await request.Website.GetChapter(p.Url);
                        return new ChapterDto()
                        {
                            Title = $"{p.Info}    {chapter.Title}",
                            Body = chapter.Body
                        };
                    }).Select(t => t.Result)
                    .Where(c => c != null)
                    .ToList()
            };
        }
    }
}