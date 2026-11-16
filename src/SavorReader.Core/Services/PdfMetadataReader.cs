using SavorReader.Core.Models;
using UglyToad.PdfPig;

namespace SavorReader.Core.Services;

public sealed class PdfMetadataReader
{
    public BookMetadata Read(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var document = PdfDocument.Open(filePath);
        var information = document.Information;

        var title = FirstNonEmpty(
            information?.Title,
            Path.GetFileNameWithoutExtension(filePath))!;

        return new BookMetadata(
            title.Trim(),
            TrimToNull(information?.Author),
            null,
            null,
            TrimToNull(information?.Subject),
            document.NumberOfPages,
            null,
            null);
    }

    private static string? FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
