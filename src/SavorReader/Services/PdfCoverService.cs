using SavorReader.Core.Models;
using SavorReader.Core.Services;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace SavorReader.Services;

public sealed class PdfCoverService
{
    private readonly AppPaths _paths;
    private readonly BookRepository _books;

    public PdfCoverService(AppPaths paths, BookRepository books)
    {
        _paths = paths;
        _books = books;
    }

    public async Task<string?> EnsureCoverAsync(
        Book book,
        CancellationToken cancellationToken = default)
    {
        if (book.Format != BookFormat.Pdf || !File.Exists(book.FilePath))
        {
            return null;
        }

        var existing = _paths.ResolveCoverPath(book.CoverPath);
        if (existing is not null && File.Exists(existing))
        {
            return book.CoverPath;
        }

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(book.FilePath);
            var document = await PdfDocument.LoadFromFileAsync(file);
            cancellationToken.ThrowIfCancellationRequested();
            if (document.PageCount == 0)
            {
                return null;
            }

            using var page = document.GetPage(0);
            using var stream = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(stream, new PdfPageRenderOptions
            {
                DestinationWidth = 360,
            });
            cancellationToken.ThrowIfCancellationRequested();

            var byteCount = checked((int)stream.Size);
            if (byteCount <= 0 || byteCount > 16 * 1024 * 1024)
            {
                return null;
            }

            stream.Seek(0);
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)byteCount);
            var bytes = new byte[byteCount];
            reader.ReadBytes(bytes);

            var fileName = $"pdf-{book.Id}.png";
            var destination = Path.Combine(_paths.CoversDirectory, fileName);
            await File.WriteAllBytesAsync(destination, bytes, cancellationToken);

            book.CoverPath = _paths.ToRelativeCoverPath(fileName);
            await _books.UpdateCoverAsync(book.Id, book.CoverPath, cancellationToken);
            return book.CoverPath;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AppLog.Warn($"PDF 封面生成失败：{book.FilePath}", exception);
            return null;
        }
    }
}
