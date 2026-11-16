namespace SavorReader.Core.Models;

public enum ReadingStatus
{
    Unread,
    Reading,
    Finished,
}

public static class ReadingStatusExtensions
{
    public static string ToStorageValue(this ReadingStatus status) => status switch
    {
        ReadingStatus.Unread => "unread",
        ReadingStatus.Reading => "reading",
        ReadingStatus.Finished => "finished",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static ReadingStatus ParseStorageValue(string value) => value.ToLowerInvariant() switch
    {
        "unread" => ReadingStatus.Unread,
        "reading" => ReadingStatus.Reading,
        "finished" => ReadingStatus.Finished,
        _ => throw new FormatException($"Unknown reading status '{value}'."),
    };
}
