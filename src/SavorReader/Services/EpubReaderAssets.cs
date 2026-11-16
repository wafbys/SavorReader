using System.Text.Json;
using System.Text.Json.Serialization;
using SavorReader.Core.Models;
using SavorReader.Core.Services;

namespace SavorReader.Services;

public sealed record EpubReaderSetup(string CacheRoot, string ShellUrl);

public sealed class EpubReaderAssets
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AppPaths _paths;
    private readonly EpubCacheService _cache;

    public EpubReaderAssets(AppPaths paths, EpubCacheService cache)
    {
        _paths = paths;
        _cache = cache;
    }

    public async Task<EpubReaderSetup> PrepareAsync(
        Book book,
        EpubBookContent content,
        ReaderSettings settings,
        CancellationToken cancellationToken = default)
    {
        var hash = book.ContentHash;
        if (string.IsNullOrWhiteSpace(hash))
        {
            hash = await FileHashService.ComputeSha256Async(book.FilePath, cancellationToken);
        }

        var cacheRoot = await _cache.EnsureExtractedAsync(book.FilePath, hash, cancellationToken);
        var readerRoot = Path.Combine(cacheRoot, "__reader");
        Directory.CreateDirectory(readerRoot);

        var assetRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Reader");
        foreach (var fileName in new[] { "reader.html", "reader.css", "reader.js" })
        {
            var source = Path.Combine(assetRoot, fileName);
            if (!File.Exists(source))
            {
                throw new FileNotFoundException($"阅读器资源缺失：{source}", source);
            }

            File.Copy(source, Path.Combine(readerRoot, fileName), overwrite: true);
        }

        object? location = book.Location is EpubLocation epubLocation
            ? new
            {
                spineIndex = Math.Max(0, epubLocation.SpineIndex),
                chapterRatio = Math.Clamp(epubLocation.ChapterRatio, 0, 1),
            }
            : null;

        var payload = new
        {
            title = content.Title,
            author = book.Author,
            spine = content.Spine
                .OrderBy(item => item.Index)
                .Select(item => item.Href)
                .ToArray(),
            toc = content.TableOfContents
                .Select(item => new
                {
                    title = item.Title,
                    depth = item.Depth,
                    spineIndex = item.SpineIndex,
                    anchor = item.Anchor,
                })
                .ToArray(),
            settings = new
            {
                theme = settings.ThemeName,
                font = settings.FontName,
                fontSize = settings.FontSize,
                lineHeight = settings.LineHeight,
                margin = settings.Margin,
            },
            location,
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(
            Path.Combine(readerRoot, "book.json"),
            json,
            cancellationToken);

        return new EpubReaderSetup(cacheRoot, "https://book.local/__reader/reader.html");
    }
}
