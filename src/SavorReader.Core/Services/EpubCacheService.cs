using System.IO.Compression;

namespace SavorReader.Core.Services;

/// <summary>
/// Extracts EPUB files into a content-addressed cache. All entry paths are checked
/// against the staging directory before anything is written (Zip Slip protection).
/// </summary>
public sealed class EpubCacheService
{
    private const long MaxTotalBytes = 2L * 1024 * 1024 * 1024;
    private const int MaxEntries = 100_000;

    private readonly AppPaths _paths;

    public EpubCacheService(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<string> EnsureExtractedAsync(
        string epubPath,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(epubPath);
        var target = _paths.GetEpubCachePath(contentHash);
        var marker = Path.Combine(target, ".ready");
        if (Directory.Exists(target) && File.Exists(marker))
        {
            return target;
        }

        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        var staging = Path.Combine(
            _paths.EpubCacheDirectory,
            $".tmp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);

        try
        {
            var totalBytes = 0L;
            var entryCount = 0;

            using var archive = ZipFile.OpenRead(epubPath);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                entryCount++;
                if (entryCount > MaxEntries)
                {
                    throw new InvalidDataException("EPUB 包含过多文件，已停止解压。");
                }

                var entryName = entry.FullName.Replace('\\', '/').TrimStart('/');
                if (entryName.Length == 0)
                {
                    continue;
                }

                var destination = Path.GetFullPath(Path.Combine(staging, entryName));
                var stagingPrefix = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
                if (!destination.StartsWith(stagingPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"EPUB 包含非法路径：{entry.FullName}");
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destination);
                    continue;
                }

                if (entry.Length > 0)
                {
                    totalBytes = checked(totalBytes + entry.Length);
                    if (totalBytes > MaxTotalBytes)
                    {
                        throw new InvalidDataException("EPUB 解压后体积过大，已停止解压。");
                    }
                }

                var parent = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                await using var source = entry.Open();
                await using var output = new FileStream(
                    destination,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    128 * 1024,
                    FileOptions.Asynchronous);
                await source.CopyToAsync(output, cancellationToken);
            }

            var container = Path.Combine(staging, "META-INF", "container.xml");
            if (!File.Exists(container))
            {
                throw new InvalidDataException("这不是有效的 EPUB：缺少 META-INF/container.xml。");
            }

            await File.WriteAllTextAsync(
                Path.Combine(staging, ".ready"),
                contentHash,
                cancellationToken);

            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }

            Directory.Move(staging, target);
            return target;
        }
        catch
        {
            try
            {
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup; the original exception is more useful.
            }

            throw;
        }
    }

    public Task DeleteAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = _paths.GetEpubCachePath(contentHash);
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(_paths.EpubCacheDirectory))
        {
            return Task.CompletedTask;
        }

        foreach (var directory in Directory.EnumerateDirectories(_paths.EpubCacheDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Delete(directory, recursive: true);
        }

        foreach (var file in Directory.EnumerateFiles(_paths.EpubCacheDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(file);
        }

        return Task.CompletedTask;
    }

    public Task<long> GetSizeBytesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(_paths.EpubCacheDirectory))
        {
            return Task.FromResult(0L);
        }

        var total = Directory
            .EnumerateFiles(_paths.EpubCacheDirectory, "*", SearchOption.AllDirectories)
            .Sum(file =>
            {
                try
                {
                    return new FileInfo(file).Length;
                }
                catch
                {
                    return 0L;
                }
            });
        return Task.FromResult(total);
    }
}
