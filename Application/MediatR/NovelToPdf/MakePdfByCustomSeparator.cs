using Application.Abstractions;
using Application.DTOs;
using Application.Helpers;
using Application.MediatR.Novle;
using MediatR;

namespace Application.MediatR.NovelToPdf;

public class MakePdfByCustomSeparator
{
    public record Query(WebSite WebSite, string Dir, string FontSize, int WhiteLinesBetweenLines,
        int NumberOfChaptersPerFile) : IRequest;

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
            var (webSite, dir, fontSize, whiteLinesBetweenLines, numberOfChaptersPerFile) = request;
            var novel = await _mediator.Send(new GetNovelWithNoChaptersSeparator.Query(webSite), cancellationToken);

            var chaptersCount = novel.Chapters.Count();
            var count = chaptersCount / (double)numberOfChaptersPerFile;
            var intCount = (int)count;
            var reminder = (int)((count - intCount) * numberOfChaptersPerFile);

            for (var i = 0; i < intCount; i++)
            {
                var chapters = novel.Chapters
                    .Skip(i * numberOfChaptersPerFile)
                    .Take(numberOfChaptersPerFile);
            
                await HandleFiles(i * numberOfChaptersPerFile + 1, (i + 1) * numberOfChaptersPerFile, dir, chapters, whiteLinesBetweenLines, fontSize);
            }

            if (reminder != 0)
            {
                var chapters = novel.Chapters.TakeLast(reminder);
                await HandleFiles(chaptersCount - reminder + 1 , chaptersCount, dir,
                    chapters,
                    whiteLinesBetweenLines, fontSize);
            }

            await JsonUtilityFunctions.WriteToFile(novel,
                Path.Combine(dir, UtilityFunctions.MakeValidFileNameFromString(novel.Name) + ".json"));
        }

        private async Task HandleFiles(int from, int to, string dir,
            IEnumerable<ChapterDto> chapters,
            int whiteLinesBetweenLines, string fontSize)
        {
            var fileName =
                $"from-{from}_to-{to}";

            var pdfFileName = $@"{dir}\{Guid.NewGuid()}.pdf";
            var newPdfFileName = $@"{dir}\{fileName}.pdf";
            var jsonFileName = $@"{dir}\JSONs\{fileName}.json";

            await _pdfMaker.MakeFromChapters(chapters, pdfFileName, whiteLinesBetweenLines, fontSize);

            File.Copy(pdfFileName, newPdfFileName, true);
            File.Delete(pdfFileName);

            UtilityFunctions.CheckDirectory(Path.Combine(dir, "JSONs"));
            await JsonUtilityFunctions.WriteToFile(chapters, jsonFileName);
        }
    }
}