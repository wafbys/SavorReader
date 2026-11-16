using SavorReader.Core.Models;

namespace SavorReader.Core.Services;

internal static class ManagedBookFiles
{
    public static string Allocate(
        string booksDirectory,
        string desiredFileName,
        string? excludePath = null,
        string? sourcePath = null)
    {
        var safeName = SanitizeFileName(desiredFileName);
        var candidate = Path.Combine(booksDirectory, safeName);
        if (CanUse(candidate, excludePath, sourcePath))
        {
            return candidate;
        }

        var stem = Path.GetFileNameWithoutExtension(safeName);
        var extension = Path.GetExtension(safeName);
        for (var suffix = 2; suffix < 10_000; suffix++)
        {
            candidate = Path.Combine(booksDirectory, $"{stem} ({suffix}){extension}");
            if (CanUse(candidate, excludePath, sourcePath))
            {
                return candidate;
            }
        }

        throw new IOException($"无法为书籍分配文件名：{safeName}");
    }

    public static string DesiredName(Book book, bool managed)
    {
        if (!managed && !LooksLikeHashName(book.FilePath))
        {
            return Path.GetFileName(book.FilePath);
        }

        var extension = Path.GetExtension(book.FilePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = book.Format == BookFormat.Pdf ? ".pdf" : ".epub";
        }

        var sourceName = book.SourcePath is { Length: > 0 } sourcePath
            && !LooksLikeHashName(sourcePath)
                ? Path.GetFileName(sourcePath)
                : null;

        return !string.IsNullOrWhiteSpace(sourceName)
            ? sourceName
            : SanitizeFileName(book.Title) + extension;
    }

    public static bool LooksLikeHashName(string filePath)
    {
        var stem = Path.GetFileNameWithoutExtension(filePath);
        return stem.Length == 64 && stem.All(char.IsAsciiHexDigit);
    }

    public static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName).Trim();
        if (name.Length == 0)
        {
            return "book";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(name.Length);
        foreach (var character in name)
        {
            builder.Append(invalid.Contains(character) ? '_' : character);
        }

        name = builder.ToString().TrimEnd(' ', '.');
        if (name.Length == 0)
        {
            return "book";
        }

        var extension = Path.GetExtension(name);
        var stem = Path.GetFileNameWithoutExtension(name);
        if (stem.Length > 180)
        {
            stem = stem[..180];
        }

        return stem + extension;
    }

    private static bool CanUse(string candidate, string? excludePath, string? sourcePath)
    {
        if (!File.Exists(candidate))
        {
            return true;
        }

        return SamePath(candidate, excludePath) || SamePath(candidate, sourcePath);
    }

    private static bool SamePath(string left, string? right)
    {
        if (string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
