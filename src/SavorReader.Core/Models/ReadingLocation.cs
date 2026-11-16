using System.Globalization;
using System.Text.Json;

namespace SavorReader.Core.Models;

public closed record class ReadingLocation;

public sealed record class EpubLocation(int SpineIndex, double ChapterRatio) : ReadingLocation;

public sealed record class PdfLocation(int PageNumber) : ReadingLocation;

public static class ReadingLocationCodec
{
    private sealed record LocationDto(string Kind, int? SpineIndex, double? ChapterRatio, int? PageNumber);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string? ToJson(ReadingLocation? location) => location switch
    {
        null => null,
        EpubLocation epub => JsonSerializer.Serialize(
            new LocationDto("epub", epub.SpineIndex, Math.Clamp(epub.ChapterRatio, 0, 1), null),
            JsonOptions),
        PdfLocation pdf => JsonSerializer.Serialize(
            new LocationDto("pdf", null, null, Math.Max(1, pdf.PageNumber)),
            JsonOptions),
    };

    public static ReadingLocation? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<LocationDto>(json, JsonOptions);
            return dto?.Kind switch
            {
                "epub" => new EpubLocation(
                    Math.Max(0, dto.SpineIndex ?? 0),
                    Math.Clamp(dto.ChapterRatio ?? 0, 0, 1)),
                "pdf" => new PdfLocation(Math.Max(1, dto.PageNumber ?? 1)),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? FormatDisplay(ReadingLocation? location) => location switch
    {
        null => null,
        EpubLocation epub => $"第 {epub.SpineIndex + 1} 节 · {epub.ChapterRatio:P0}",
        PdfLocation pdf => $"第 {pdf.PageNumber} 页",
    };

    public static double NormalizeProgress(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;

    public static string FormatPercent(double value) =>
        (NormalizeProgress(value) * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
}
