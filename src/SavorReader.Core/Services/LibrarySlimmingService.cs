using SavorReader.Core.Models;

namespace SavorReader.Core.Services;

/// <summary>
/// Reclaims derived data without touching books or covers currently referenced by the library.
/// WebView2's profile is marked for removal at next startup because Chromium may still hold files.
/// </summary>
public sealed class LibrarySlimmingService
{
    private readonly AppPaths _paths;
    private readonly BookRepository _books;

    public LibrarySlimmingService(AppPaths paths, BookRepository books)
    {
        _paths = paths;
        _books = books;
    }

    public async Task<CleanupReport> ScanAsync(CancellationToken cancellationToken = default)
    {
        var scan = await BuildScanAsync(cancellationToken);
        var targets = new List<CleanupTarget>
        {
            CreateTarget("epub-cache", "EPUB 解压缓存", scan.CacheFiles, CleanupTargetKind.Immediate),
            CreateTarget("orphan-books", "未引用的书籍副本", scan.OrphanBooks, CleanupTargetKind.Immediate),
            CreateTarget("orphan-covers", "未引用的封面", scan.OrphanCovers, CleanupTargetKind.Immediate),
            CreateTarget("logs", "日志", scan.LogFiles, CleanupTargetKind.Immediate),
            CreateTarget("webview2", "WebView2 浏览器缓存", scan.WebViewFiles, CleanupTargetKind.AfterRestart),
        };

        var immediateBytes = targets
            .Where(target => target.Kind == CleanupTargetKind.Immediate)
            .Sum(target => target.Bytes);
        var deferredBytes = targets
            .Where(target => target.Kind == CleanupTargetKind.AfterRestart)
            .Sum(target => target.Bytes);

        return new CleanupReport(targets, immediateBytes, deferredBytes);
    }

    public async Task<CleanupResult> CleanAsync(
        bool includeWebView2 = true,
        CancellationToken cancellationToken = default)
    {
        var scan = await BuildScanAsync(cancellationToken);
        var errors = new List<string>();
        var removedBytes = 0L;
        var removedFiles = 0;

        foreach (var directory in new[] { _paths.EpubCacheDirectory, _paths.LogsDirectory })
        {
            var files = directory == _paths.EpubCacheDirectory ? scan.CacheFiles : scan.LogFiles;
            foreach (var file in files)
            {
                var bytes = SafeLength(file);
                if (DeleteFile(file, errors))
                {
                    removedBytes += bytes;
                    removedFiles++;
                }
            }
        }

        foreach (var file in scan.OrphanBooks.Concat(scan.OrphanCovers))
        {
            var bytes = SafeLength(file);
            if (DeleteFile(file, errors))
            {
                removedBytes += bytes;
                removedFiles++;
            }
        }

        RemoveEmptyDirectories(_paths.EpubCacheDirectory);
        RemoveEmptyDirectories(_paths.LogsDirectory);

        var scheduledBytes = 0L;
        if (includeWebView2 && scan.WebViewFiles.Count > 0)
        {
            try
            {
                Directory.CreateDirectory(_paths.DataRoot);
                await File.WriteAllTextAsync(
                    _paths.DeferredWebViewCleanupMarker,
                    DateTimeOffset.UtcNow.ToString("O"),
                    cancellationToken);
                scheduledBytes = scan.WebViewFiles.Sum(SafeLength);
            }
            catch (Exception exception)
            {
                errors.Add($"标记 WebView2 缓存清理失败：{exception.Message}");
            }
        }

        return new CleanupResult(removedBytes, removedFiles, scheduledBytes, errors);
    }

    public Task<bool> ApplyDeferredWebViewCleanupAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var marker = _paths.DeferredWebViewCleanupMarker;
        if (!File.Exists(marker))
        {
            return Task.FromResult(false);
        }

        try
        {
            if (Directory.Exists(_paths.WebView2Directory))
            {
                Directory.Delete(_paths.WebView2Directory, recursive: true);
            }

            File.Delete(marker);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private async Task<ScanData> BuildScanAsync(CancellationToken cancellationToken)
    {
        var books = await _books.GetAllAsync(cancellationToken);
        var referencedBooks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referencedCovers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var book in books)
        {
            if (!string.IsNullOrWhiteSpace(book.FilePath))
            {
                referencedBooks.Add(Path.GetFullPath(book.FilePath));
            }

            var coverPath = _paths.ResolveCoverPath(book.CoverPath);
            if (!string.IsNullOrWhiteSpace(coverPath))
            {
                referencedCovers.Add(Path.GetFullPath(coverPath));
            }
        }

        var bookFiles = EnumerateFilesSafe(_paths.BooksDirectory);
        var coverFiles = EnumerateFilesSafe(_paths.CoversDirectory);

        return new ScanData(
            EnumerateFilesSafe(_paths.EpubCacheDirectory),
            bookFiles.Where(file => !referencedBooks.Contains(file.FullName)).ToList(),
            coverFiles.Where(file => !referencedCovers.Contains(file.FullName)).ToList(),
            EnumerateFilesSafe(_paths.LogsDirectory),
            EnumerateFilesSafe(_paths.WebView2Directory));
    }

    private static CleanupTarget CreateTarget(
        string key,
        string name,
        IReadOnlyCollection<FileInfo> files,
        CleanupTargetKind kind) =>
        new(key, name, files.Sum(SafeLength), files.Count, kind);

    private static List<FileInfo> EnumerateFilesSafe(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            };
            return new DirectoryInfo(directory)
                .EnumerateFiles("*", options)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static bool DeleteFile(FileInfo file, List<string> errors)
    {
        try
        {
            if (file.Exists)
            {
                file.Delete();
            }

            return true;
        }
        catch (Exception exception)
        {
            errors.Add($"{file.FullName}：{exception.Message}");
            return false;
        }
    }

    private static long SafeLength(FileInfo file)
    {
        try
        {
            return file.Length;
        }
        catch
        {
            return 0;
        }
    }

    private static void RemoveEmptyDirectories(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        try
        {
            foreach (var directory in Directory
                         .EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                         .OrderByDescending(path => path.Length))
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
        }
        catch
        {
            // Best-effort cleanup; the next scan reports anything left behind.
        }
    }

    private sealed record ScanData(
        IReadOnlyList<FileInfo> CacheFiles,
        IReadOnlyList<FileInfo> OrphanBooks,
        IReadOnlyList<FileInfo> OrphanCovers,
        IReadOnlyList<FileInfo> LogFiles,
        IReadOnlyList<FileInfo> WebViewFiles);
}
