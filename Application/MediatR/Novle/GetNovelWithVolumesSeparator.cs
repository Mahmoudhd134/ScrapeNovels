using Application.Abstractions;
using Application.DTOs;
using Domain.NovelModels;
using MediatR;

namespace Application.MediatR.Novle;

public class GetNovelWithVolumesSeparator
{
    public record Query(WebSite WebSite) : IRequest<NovelDto>;

    public class Handler : IRequestHandler<Query, NovelDto>
    {
        public async Task<NovelDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var volumes = await request.WebSite.GetVolumePages();
            return new NovelDto
            {
                Name = await request.WebSite.GetNovelName(),
                Volumes = volumes.Select(volume => new VolumeDto()
                {
                    Title = volume.Title,
                    Chapters = volume.Chapters.Select(async ch =>
                        {
                            var chapter = await request.WebSite.GetChapter(ch.Url);
                            return new ChapterDto()
                            {
                                Title = $"{ch.Info}   {chapter.Title}",
                                Body = chapter.Body
                            };
                        })
                        .Select(t => t.Result)
                        .Where(c => c != null)
                        .ToList()
                }).ToList()
            };
        }
    }
}