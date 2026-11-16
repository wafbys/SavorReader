namespace SavorReader.Core.Models;

public sealed class Book
{
    public long Id { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public BookFormat Format { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Author { get; set; }

    public string? Publisher { get; set; }

    public string? Language { get; set; }

    public string? Description { get; set; }

    /// <summary>Path relative to the application data root, for example <c>covers/abc.jpg</c>.</summary>
    public string? CoverPath { get; set; }

    public int? PageCount { get; set; }

    public long FileSize { get; set; }

    public DateTimeOffset FileModifiedUtc { get; set; }

    public string? ContentHash { get; set; }

    public DateTimeOffset AddedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastOpenedAtUtc { get; set; }

    public ReadingStatus Status { get; set; } = ReadingStatus.Unread;

    /// <summary>Normalized reading progress in the range 0..1.</summary>
    public double Progress { get; set; }

    public ReadingLocation? Location { get; set; }

    public List<string> Tags { get; set; } = [];

    public override string ToString() => $"{Title} ({Format})";
}
