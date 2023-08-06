using Application.Abstractions;
using Application.Helpers;
using Application.MediatR.Novle;
using MediatR;

namespace Application.MediatR.NovelToPdf;

public class MakePdfByVolumeSeparators
{
    public record Query(WebSite WebSite, string Dir, string FontSize, int WhiteLinesBetweenLines) : IRequest;

    public class Handler : IRequestHandler<Query>
    {
        private readonly IMediator _mediator;
        private readonly IPdfMaker _pdfMaker;

        public Handler(IMediator mediator, IPdfMaker pdfMaker)
        {
            _mediator = mediator;
            _pdfMaker = pdfMaker;
        }

        public async Task Handle(Query request, CancellationToken cancellationToken)
        {
            var (webSite, dir, fontSize, whiteLinesBetweenLines) = request;
            var novel = await _mediator.Send(new GetNovelWithVolumesSeparator.Query(webSite), cancellationToken);

            var lastChapter = 0;
            foreach (var (volume, i) in novel.Volumes.Select((v, i) => (v, i)))
            {
                var guid = Guid.NewGuid();
                var fileName =
                    $"{i + 1}.{volume.Title}_from-{lastChapter + 1}_to-{lastChapter += volume.Chapters.Count()}";

                var pdfFileName = $@"{dir}\{guid}.pdf";
                var newPdfFileName = $@"{dir}\{fileName}.pdf";
                var jsonFileName = $@"{dir}\JSONs\{fileName}.json";

                await _pdfMaker.MakeFromChapters(volume.Chapters, pdfFileName, whiteLinesBetweenLines, fontSize);

                File.Copy(pdfFileName, newPdfFileName, true);
                File.Delete(pdfFileName);

                UtilityFunctions.CheckDirectory(Path.Combine(dir, "JSONs"));
                await JsonUtilityFunctions.WriteToFile(volume, jsonFileName);
            }

            await JsonUtilityFunctions.WriteToFile(novel,
                Path.Combine(dir, UtilityFunctions.MakeValidFileNameFromString(novel.Name) + ".json"));
        }
    }
}