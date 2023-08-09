using Application.DTOs;
using Application.DTOs.Novel;
using Domain.Websites;
using MediatR;

namespace Application.MediatR.Novle;

public class GetChapter
{
    public record Query(NovelWebsite Website, string Url) : IRequest<ChapterDto>;

    public class Handler : IRequestHandler<Query, ChapterDto>
    {
        public async Task<ChapterDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var chapter = await request.Website.GetChapter(request.Url);
            return new ChapterDto()
            {
                Title = chapter.Title,
                Body = chapter.Body
            };
        }
    }
}