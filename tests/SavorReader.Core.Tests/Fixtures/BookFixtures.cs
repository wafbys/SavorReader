using System.IO.Compression;
using System.Text;

namespace SavorReader.Core.Tests;

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "SavorReader.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // Best effort for test cleanup.
        }
    }
}

internal static class BookFixtures
{
    private const string CoverPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    public static string CreateEpub(string path, bool includeCover = true, bool addZipSlipEntry = false)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteText(archive, "mimetype", "application/epub+zip", CompressionLevel.NoCompression);
        WriteText(archive, "META-INF/container.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
              <rootfiles>
                <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
              </rootfiles>
            </container>
            """);

        var coverManifest = includeCover
            ? """<item id="cover" href="Images/cover.png" media-type="image/png" properties="cover-image"/>"""
            : string.Empty;

        WriteText(archive, "OEBPS/content.opf", $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="bookid">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                <dc:identifier id="bookid">savor-reader-test</dc:identifier>
                <dc:title>测试书</dc:title>
                <dc:creator>测试作者</dc:creator>
                <dc:language>zh-CN</dc:language>
                <dc:publisher>测试出版社</dc:publisher>
                <dc:description>用于单元测试的示例书籍。</dc:description>
                <meta property="dcterms:modified">2026-01-01T00:00:00Z</meta>
              </metadata>
              <manifest>
                <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                <item id="ch1" href="Text/ch1.xhtml" media-type="application/xhtml+xml"/>
                <item id="ch2" href="Text/ch2.xhtml" media-type="application/xhtml+xml"/>
                <item id="css" href="Styles/style.css" media-type="text/css"/>
                {{coverManifest}}
              </manifest>
              <spine>
                <itemref idref="ch1"/>
                <itemref idref="ch2"/>
              </spine>
            </package>
            """);

        WriteText(archive, "OEBPS/nav.xhtml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE html>
            <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops" lang="zh-CN">
            <head><meta charset="utf-8"/><title>目录</title></head>
            <body>
              <nav epub:type="toc" id="toc">
                <ol>
                  <li><a href="Text/ch1.xhtml">第一章</a>
                    <ol><li><a href="Text/ch1.xhtml#start">开篇</a></li></ol>
                  </li>
                  <li><a href="Text/ch2.xhtml">第二章</a></li>
                </ol>
              </nav>
            </body>
            </html>
            """);

        WriteText(archive, "OEBPS/Text/ch1.xhtml", ChapterHtml("第一章", "start"));
        WriteText(archive, "OEBPS/Text/ch2.xhtml", ChapterHtml("第二章", null));
        WriteText(archive, "OEBPS/Styles/style.css", "body { font-family: sans-serif; }");

        if (includeCover)
        {
            var entry = archive.CreateEntry("OEBPS/Images/cover.png", CompressionLevel.Optimal);
            using var stream = entry.Open();
            stream.Write(Convert.FromBase64String(CoverPngBase64));
        }

        if (addZipSlipEntry)
        {
            WriteText(archive, "../zip-slip.txt", "must not escape", CompressionLevel.Optimal);
        }

        return path;
    }

    public static string CreatePdf(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var content = "BT /F1 18 Tf 50 750 Td (Hello PDF) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
            "<< /Title (Test PDF) /Author (Test Author) /Subject (PDF fixture) >>",
        };

        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(index + 1).Append(" 0 obj\n");
            builder.Append(objects[index]).Append("\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n0 ").Append(objects.Length + 1).Append('\n');
        builder.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            builder.Append(offset.ToString("D10")).Append(" 00000 n \n");
        }

        builder.Append("trailer\n<< /Size ").Append(objects.Length + 1)
            .Append(" /Root 1 0 R /Info 6 0 R >>\nstartxref\n")
            .Append(xrefOffset).Append("\n%%EOF");

        File.WriteAllText(path, builder.ToString(), Encoding.ASCII);
        return path;
    }

    private static string ChapterHtml(string title, string? anchor)
    {
        var id = anchor is null ? string.Empty : $" id=\"{anchor}\"";
        return $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE html>
            <html xmlns="http://www.w3.org/1999/xhtml" lang="zh-CN">
            <head><meta charset="utf-8"/><title>{{title}}</title><link rel="stylesheet" href="../Styles/style.css"/></head>
            <body><h1{{id}}>{{title}}</h1><p>这是用于测试的正文。</p></body>
            </html>
            """;
    }

    private static void WriteText(
        ZipArchive archive,
        string name,
        string content,
        CompressionLevel level = CompressionLevel.Optimal)
    {
        var entry = archive.CreateEntry(name, level);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
