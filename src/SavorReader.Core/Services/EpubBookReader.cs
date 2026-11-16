using System.Text.RegularExpressions;
using SavorReader.Core.Models;
using VersOne.Epub;

namespace SavorReader.Core.Services;

public sealed class EpubBookReader
{
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);

    public EpubReadResult Read(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var epub = EpubReader.ReadBook(filePath);
        var package = epub.Schema?.Package;
        var metadata = package?.Metadata;

        var title = FirstNonEmpty(
            epub.Title,
            metadata?.Titles.FirstOrDefault()?.Title,
            Path.GetFileNameWithoutExtension(filePath))!;

        var author = FirstNonEmpty(epub.Author)
            ?? (epub.AuthorList is { Count: > 0 } ? string.Join(", ", epub.AuthorList) : null);

        var publisher = metadata?.Publishers.FirstOrDefault()?.Publisher;
        var language = FirstNonEmpty(package?.Language, metadata?.Languages.FirstOrDefault()?.Language);
        var description = StripHtml(FirstNonEmpty(
            epub.Description,
            metadata?.Descriptions.FirstOrDefault()?.Description));

        var coverBytes = epub.CoverImage is { Length: > 0 } ? epub.CoverImage : null;
        var coverExtension = DetectImageExtension(coverBytes, epub.Content?.Cover?.ContentMimeType);

        var spine = new List<EpubSpineItem>();
        var spineIndexByPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < epub.ReadingOrder.Count; index++)
        {
            var file = epub.ReadingOrder[index];
            var href = NormalizePath(file.FilePath);
            if (href.Length == 0)
            {
                continue;
            }

            var spineIndex = spine.Count;
            spine.Add(new EpubSpineItem(spineIndex, href, Path.GetFileNameWithoutExtension(href)));
            spineIndexByPath[PathKey(href)] = spineIndex;
        }

        var toc = FlattenNavigation(epub.Navigation, spineIndexByPath);
        if (toc.Count == 0)
        {
            toc = spine
                .Select(item => new EpubTocEntry(item.Title, 0, item.Index, null))
                .ToList();
        }

        // Use the first TOC label that points at each spine item as its display title.
        var titleBySpine = toc
            .GroupBy(entry => entry.SpineIndex)
            .ToDictionary(group => group.Key, group => group.First().Title);
        spine = spine
            .Select(item => titleBySpine.TryGetValue(item.Index, out var spineTitle) && !string.IsNullOrWhiteSpace(spineTitle)
                ? item with { Title = spineTitle }
                : item)
            .ToList();

        var bookMetadata = new BookMetadata(
            title.Trim(),
            TrimToNull(author),
            TrimToNull(publisher),
            TrimToNull(language),
            TrimToNull(description),
            null,
            coverBytes,
            coverExtension);

        return new EpubReadResult(
            bookMetadata,
            new EpubBookContent(title.Trim(), spine, toc));
    }

    private static List<EpubTocEntry> FlattenNavigation(
        IReadOnlyList<VersOne.Epub.EpubNavigationItem>? navigation,
        IReadOnlyDictionary<string, int> spineIndexByPath)
    {
        var result = new List<EpubTocEntry>();
        if (navigation is null)
        {
            return result;
        }

        foreach (var item in navigation)
        {
            Visit(item, 0);
        }

        return result;

        void Visit(VersOne.Epub.EpubNavigationItem item, int depth)
        {
            var path = FirstNonEmpty(
                item.Link?.ContentFilePath,
                item.HtmlContentFile?.FilePath);
            var anchor = item.Link?.Anchor;
            if (path is null && !string.IsNullOrWhiteSpace(anchor))
            {
                // A pure fragment points into the previous file; leave it out of the flat list.
                path = null;
            }

            if (path is not null)
            {
                if (NormalizePath(path).Contains('#'))
                {
                    var parts = NormalizePath(path).Split('#', 2);
                    path = parts[0];
                    anchor = string.IsNullOrWhiteSpace(anchor) && parts.Length == 2 ? parts[1] : anchor;
                }

                if (spineIndexByPath.TryGetValue(PathKey(path), out var spineIndex))
                {
                    result.Add(new EpubTocEntry(
                        string.IsNullOrWhiteSpace(item.Title) ? $"第 {spineIndex + 1} 节" : item.Title.Trim(),
                        depth,
                        spineIndex,
                        TrimToNull(anchor)));
                }
            }

            if (item.NestedItems is { Count: > 0 })
            {
                foreach (var nested in item.NestedItems)
                {
                    Visit(nested, depth + 1);
                }
            }
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/').Trim().TrimStart('/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        var fragmentIndex = normalized.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            normalized = normalized[..fragmentIndex];
        }

        return normalized;
    }

    private static string PathKey(string path)
    {
        var normalized = NormalizePath(path);
        try
        {
            return Uri.UnescapeDataString(normalized).ToLowerInvariant();
        }
        catch (UriFormatException)
        {
            return normalized.ToLowerInvariant();
        }
    }

    private static string? DetectImageExtension(byte[]? bytes, string? mimeType)
    {
        if (bytes is not { Length: > 0 })
        {
            return null;
        }

        var mime = mimeType?.ToLowerInvariant() ?? string.Empty;
        if (mime.Contains("jpeg", StringComparison.Ordinal) || mime.Contains("jpg", StringComparison.Ordinal))
        {
            return ".jpg";
        }

        if (mime.Contains("png", StringComparison.Ordinal))
        {
            return ".png";
        }

        if (mime.Contains("gif", StringComparison.Ordinal))
        {
            return ".gif";
        }

        if (mime.Contains("webp", StringComparison.Ordinal))
        {
            return ".webp";
        }

        if (mime.Contains("bmp", StringComparison.Ordinal))
        {
            return ".bmp";
        }

        if (mime.Contains("svg", StringComparison.Ordinal))
        {
            // WinUI's BitmapImage does not decode SVG; use the placeholder cover instead.
            return null;
        }

        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return ".png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return ".jpg";
        }

        if (bytes.Length >= 6
            && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
        {
            return ".gif";
        }

        if (bytes.Length >= 12
            && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return ".webp";
        }

        if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
        {
            return ".bmp";
        }

        return null;
    }

    private static string? StripHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var withoutTags = HtmlTagRegex.Replace(text, " ");
        var decoded = System.Net.WebUtility.HtmlDecode(withoutTags);
        var normalized = Regex.Replace(decoded, @"\s+", " ").Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    private static string? FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
