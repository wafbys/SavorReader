namespace SavorReader.Core.Models;

/// <summary>
/// Storage shape for future highlights, notes, and bookmarks. No UI is wired up in the MVP.
/// </summary>
public sealed class Annotation
{
    public long Id { get; set; }

    public long BookId { get; set; }

    public string Kind { get; set; } = "highlight";

    public string LocationJson { get; set; } = string.Empty;

    public string? SelectedText { get; set; }

    public string? Note { get; set; }

    public string? Color { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
