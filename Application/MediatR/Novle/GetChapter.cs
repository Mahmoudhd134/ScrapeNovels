using Application.Abstractions;
using Application.DTOs;
using MediatR;

namespace Application.MediatR.Novle;

public class GetChapter
{
    public record Query(WebSite WebSite, string Url) : IRequest<ChapterDto>;

    public class Handler : IRequestHandler<Query, ChapterDto>
    {
        public async Task<ChapterDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var chapter = await request.WebSite.GetChapter(request.Url);
            return new ChapterDto()
            {
                Title = chapter.Title,
                Body = chapter.Body
            };
        }
    }
}