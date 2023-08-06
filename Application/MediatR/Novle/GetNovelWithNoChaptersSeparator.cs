using Application.Abstractions;
using Application.DTOs;
using MediatR;

namespace Application.MediatR.Novle;

public class GetNovelWithNoChaptersSeparator
{
    public record Query(WebSite WebSite) : IRequest<NovelWithNoVolumesDto>;

    public class Handler : IRequestHandler<Query, NovelWithNoVolumesDto>
    {
        public async Task<NovelWithNoVolumesDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var pages = await request.WebSite.GetAllPages();
            return new NovelWithNoVolumesDto()
            {
                Name = await request.WebSite.GetNovelName(),
                Chapters = pages.Select(async p =>
                    {
                        var chapter = await request.WebSite.GetChapter(p.Url);
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