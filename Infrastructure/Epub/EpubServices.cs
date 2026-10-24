using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Application.DTOs.Novel;
using Application.Helpers;
using Application.Interfaces;

namespace Infrastructure.Epub;

// Writes an EPUB 3 package by hand (with an EPUB 2 toc.ncx for older readers), so the table of
// contents can nest chapters under volumes and the reading direction follows the novel's language.
public class EpubServices : IEpubServices
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public Task MakeEpub(NovelDto novel, string outputPath) =>
        Write(novel.Name, novel.Volumes.Select(v => new Section(v.Title, v.Chapters)).ToList(), outputPath);

    public Task MakeEpub(NovelWithNoVolumesDto novel, string outputPath) =>
        Write(novel.Name, [new Section(null, novel.Chapters)], outputPath);

    // A section with a null title is a run of chapters with no volume around it.
    private record Section(string Title, IList<ChapterDto> Chapters);

    private record Page(string Id, string FileName, string Title, string Html);

    private record TocEntry(string Title, string FileName, List<TocEntry> Children);

    private static async Task Write(string name, List<Section> sections, string outputPath)
    {
        UtilityFunctions.CheckDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));

        name = string.IsNullOrWhiteSpace(name) ? "Novel" : name.Trim();
        var (lang, dir) = DetectLanguage(name, sections);

        var pages = new List<Page> { new("title", "title.xhtml", name, $"<h1 class=\"book-title\">{Xml(name)}</h1>") };
        var toc = new List<TocEntry>();
        var chapterNumber = 0;

        foreach (var (section, sectionIndex) in sections.Select((s, i) => (s, i + 1)))
        {
            var entries = toc;
            if (section.Title != null)
            {
                var volumeTitle = string.IsNullOrWhiteSpace(section.Title) ? $"Volume {sectionIndex}" : section.Title.Trim();
                var volumePage = new Page($"vol{sectionIndex:D3}", $"vol{sectionIndex:D3}.xhtml", volumeTitle,
                    $"<h1 class=\"volume-title\">{Xml(volumeTitle)}</h1>");
                pages.Add(volumePage);

                var volumeEntry = new TocEntry(volumeTitle, volumePage.FileName, []);
                toc.Add(volumeEntry);
                entries = volumeEntry.Children;
            }

            foreach (var chapter in section.Chapters ?? [])
            {
                chapterNumber++;
                var title = string.IsNullOrWhiteSpace(chapter.Title) ? $"Chapter {chapterNumber}" : chapter.Title.Trim();
                var body = string.Concat((chapter.Body ?? []).Select(p => $"<p>{Xml(p)}</p>\n"));
                var page = new Page($"ch{chapterNumber:D5}", $"ch{chapterNumber:D5}.xhtml", title,
                    $"<h2>{Xml(title)}</h2>\n{body}");

                pages.Add(page);
                entries.Add(new TocEntry(title, page.FileName, []));
            }
        }

        var bookId = $"urn:uuid:{IdFromName(name)}";

        await using var file = new FileStream(outputPath, FileMode.Create);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);

        // The mimetype entry must come first and be stored uncompressed.
        await AddEntry(zip, "mimetype", "application/epub+zip", CompressionLevel.NoCompression);
        await AddEntry(zip, "META-INF/container.xml", ContainerXml);
        await AddEntry(zip, "OEBPS/style.css", Css(dir));
        await AddEntry(zip, "OEBPS/content.opf", Opf(bookId, name, lang, dir, pages));
        await AddEntry(zip, "OEBPS/nav.xhtml", Nav(name, lang, dir, toc));
        await AddEntry(zip, "OEBPS/toc.ncx", Ncx(bookId, name, toc));

        foreach (var page in pages)
            await AddEntry(zip, "OEBPS/" + page.FileName, Xhtml(page.Title, lang, dir, page.Html));
    }

    private static async Task AddEntry(ZipArchive zip, string path, string content,
        CompressionLevel level = CompressionLevel.Optimal)
    {
        await using var writer = new StreamWriter(zip.CreateEntry(path, level).Open(), Utf8NoBom);
        await writer.WriteAsync(content);
    }

    private static string Xhtml(string title, string lang, string dir, string body) =>
        $"""
         <?xml version="1.0" encoding="utf-8"?>
         <!DOCTYPE html>
         <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops" xml:lang="{lang}" lang="{lang}" dir="{dir}">
         <head>
         <title>{Xml(title)}</title>
         <link rel="stylesheet" type="text/css" href="style.css"/>
         </head>
         <body dir="{dir}">
         {body}
         </body>
         </html>
         """;

    private static string Opf(string bookId, string name, string lang, string dir, List<Page> pages)
    {
        var manifest = string.Concat(pages.Select(p =>
            $"    <item id=\"{p.Id}\" href=\"{p.FileName}\" media-type=\"application/xhtml+xml\"/>\n"));
        var spine = string.Concat(pages.Select(p => $"    <itemref idref=\"{p.Id}\"/>\n"));
        var modified = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        return $"""
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="book-id" xml:lang="{lang}" dir="{dir}">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="book-id">{bookId}</dc:identifier>
                    <dc:title>{Xml(name)}</dc:title>
                    <dc:language>{lang}</dc:language>
                    <meta property="dcterms:modified">{modified}</meta>
                  </metadata>
                  <manifest>
                    <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                    <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>
                    <item id="css" href="style.css" media-type="text/css"/>
                {manifest}  </manifest>
                  <spine toc="ncx" page-progression-direction="{dir}">
                {spine}  </spine>
                </package>
                """;
    }

    private static string Nav(string name, string lang, string dir, List<TocEntry> toc)
    {
        var list = new StringBuilder();
        AppendNavList(list, toc);

        return Xhtml(name, lang, dir, $"""
                                       <nav epub:type="toc" id="toc">
                                       <h1>{Xml(name)}</h1>
                                       {list}</nav>
                                       """);
    }

    private static void AppendNavList(StringBuilder sb, List<TocEntry> entries)
    {
        sb.Append("<ol>\n");
        foreach (var entry in entries)
        {
            sb.Append($"<li><a href=\"{entry.FileName}\">{Xml(entry.Title)}</a>");
            if (entry.Children.Count > 0)
                AppendNavList(sb, entry.Children);
            sb.Append("</li>\n");
        }

        sb.Append("</ol>\n");
    }

    private static string Ncx(string bookId, string name, List<TocEntry> toc)
    {
        var navMap = new StringBuilder();
        var playOrder = 0;
        AppendNavPoints(navMap, toc, ref playOrder);

        return $"""
                <?xml version="1.0" encoding="utf-8"?>
                <ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1">
                  <head>
                    <meta name="dtb:uid" content="{bookId}"/>
                    <meta name="dtb:depth" content="{(toc.Any(e => e.Children.Count > 0) ? 2 : 1)}"/>
                    <meta name="dtb:totalPageCount" content="0"/>
                    <meta name="dtb:maxPageNumber" content="0"/>
                  </head>
                  <docTitle><text>{Xml(name)}</text></docTitle>
                  <navMap>
                {navMap}  </navMap>
                </ncx>
                """;
    }

    private static void AppendNavPoints(StringBuilder sb, List<TocEntry> entries, ref int playOrder)
    {
        foreach (var entry in entries)
        {
            playOrder++;
            sb.Append($"<navPoint id=\"np{playOrder}\" playOrder=\"{playOrder}\">" +
                      $"<navLabel><text>{Xml(entry.Title)}</text></navLabel>" +
                      $"<content src=\"{entry.FileName}\"/>\n");
            AppendNavPoints(sb, entry.Children, ref playOrder);
            sb.Append("</navPoint>\n");
        }
    }

    // Arabic text (the translated novels) reads right-to-left; the scraped Chinese originals don't.
    private static (string Lang, string Dir) DetectLanguage(string name, List<Section> sections)
    {
        var sample = string.Concat(sections
            .SelectMany(s => s.Chapters ?? [])
            .Take(5)
            .SelectMany(c => (c.Body ?? []).Take(20).Prepend(c.Title ?? ""))
            .Prepend(name));

        var arabic = sample.Count(c => c is >= '؀' and <= 'ۿ');
        var chinese = sample.Count(c => c is >= '一' and <= '鿿');

        if (arabic > 0 && arabic >= chinese)
            return ("ar", "rtl");

        return chinese > 0 ? ("zh", "ltr") : ("en", "ltr");
    }

    // Same novel name, same id, so readers treat a re-export as the same book.
    private static Guid IdFromName(string name) => new(MD5.HashData(Encoding.UTF8.GetBytes(name)));

    // Escapes markup and drops characters XML can't hold (stray control chars from scraped pages).
    private static string Xml(string text) =>
        SecurityElement.Escape(new string((text ?? "").Where(IsValidXmlChar).ToArray()));

    private static bool IsValidXmlChar(char c) =>
        c is '\t' or '\n' or '\r' || (c >= 0x20 && c != 0xFFFE && c != 0xFFFF);

    private const string ContainerXml = """
                                        <?xml version="1.0" encoding="utf-8"?>
                                        <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                                          <rootfiles>
                                            <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
                                          </rootfiles>
                                        </container>
                                        """;

    // Some readers (e.g. Aquile Reader) ignore the dir attribute and only honour CSS direction.
    private static string Css(string dir) =>
        $$"""
          html, body { direction: {{dir}}; unicode-bidi: embed; }
          body { line-height: 1.8; text-align: {{(dir == "rtl" ? "right" : "left")}}; }
          p { margin: 0 0 0.8em 0; text-indent: 0; }
          h2 { text-align: center; margin: 1em 0 1.5em 0; }
          .book-title, .volume-title { text-align: center; margin-top: 35%; }
          """;
}
