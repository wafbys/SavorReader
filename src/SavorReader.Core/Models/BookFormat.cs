namespace SavorReader.Core.Models;

public enum BookFormat
{
    Epub,
    Pdf,
}

public static class BookFormatExtensions
{
    public static string ToStorageValue(this BookFormat format) => format switch
    {
        BookFormat.Epub => "epub",
        BookFormat.Pdf => "pdf",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static BookFormat ParseStorageValue(string value) => value.ToLowerInvariant() switch
    {
        "epub" => BookFormat.Epub,
        "pdf" => BookFormat.Pdf,
        _ => throw new FormatException($"Unknown book format '{value}'."),
    };

    public static BookFormat? FromPath(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".epub" => BookFormat.Epub,
        ".pdf" => BookFormat.Pdf,
        _ => null,
    };
}
