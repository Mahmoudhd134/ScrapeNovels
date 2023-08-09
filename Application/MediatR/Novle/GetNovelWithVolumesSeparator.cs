using Application.DTOs;
using Application.DTOs.Novel;
using Domain.NovelModels;
using Domain.Websites;
using MediatR;

namespace Application.MediatR.Novle;

public class GetNovelWithVolumesSeparator
{
    public record Query(NovelWebsite Website,string BaseUrl) : IRequest<NovelDto>;

    public class Handler : IRequestHandler<Query, NovelDto>
    {
        public async Task<NovelDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var volumes = await request.Website.GetVolumePages(request.BaseUrl);
            return new NovelDto
            {
                Name = await request.Website.GetNovelName(request.BaseUrl),
                Volumes = volumes.Select(volume => new VolumeDto()
                {
                    Title = volume.Title,
                    Chapters = volume.Chapters.Select(async ch =>
                        {
                            var chapter = await request.Website.GetChapter(ch.Url);
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