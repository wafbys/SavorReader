using SavorReader.Core.Models;

namespace SavorReader.Core.Services;

public sealed class ReadingProgressService
{
    private readonly BookRepository _books;

    public ReadingProgressService(BookRepository books)
    {
        _books = books;
    }

    public async Task MarkOpenedAsync(
        Book book,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        await _books.MarkOpenedAsync(book.Id, now, cancellationToken);
        book.LastOpenedAtUtc = now;
        if (book.Status == ReadingStatus.Unread)
        {
            book.Status = ReadingStatus.Reading;
        }
    }

    public async Task SaveEpubProgressAsync(
        Book book,
        int spineIndex,
        double chapterRatio,
        double overallProgress,
        CancellationToken cancellationToken = default)
    {
        var progress = ReadingLocationCodec.NormalizeProgress(overallProgress);
        var status = progress >= 0.995
            ? ReadingStatus.Finished
            : book.Status == ReadingStatus.Finished
                ? ReadingStatus.Finished
                : ReadingStatus.Reading;

        var location = new EpubLocation(Math.Max(0, spineIndex), Math.Clamp(chapterRatio, 0, 1));
        var now = DateTimeOffset.UtcNow;
        await _books.UpdateProgressAsync(book.Id, status, progress, location, now, cancellationToken);

        book.Progress = progress;
        book.Status = status;
        book.Location = location;
        book.LastOpenedAtUtc = now;
    }

    public async Task SetStatusAsync(
        Book book,
        ReadingStatus status,
        CancellationToken cancellationToken = default)
    {
        var progress = status switch
        {
            ReadingStatus.Finished => 1,
            ReadingStatus.Unread => 0,
            _ => book.Progress,
        };
        var now = DateTimeOffset.UtcNow;
        await _books.UpdateProgressAsync(book.Id, status, progress, book.Location, now, cancellationToken);
        book.Status = status;
        book.Progress = progress;
        book.LastOpenedAtUtc = now;
    }
}
