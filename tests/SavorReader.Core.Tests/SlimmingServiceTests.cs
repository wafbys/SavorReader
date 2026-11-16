using SavorReader.Core.Models;
using SavorReader.Core.Services;

namespace SavorReader.Core.Tests;

public sealed class SlimmingServiceTests
{
    [Fact]
    public async Task Slimming_RemovesDerivedDataButKeepsCurrentBookAndCover()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(Path.Combine(temp.Path, "data"));
        var database = new LibraryDatabase(paths.DatabasePath);
        await database.InitializeAsync();
        var repository = new BookRepository(database);

        var currentBookPath = Path.Combine(paths.BooksDirectory, "current.epub");
        var currentCoverPath = Path.Combine(paths.CoversDirectory, "current.jpg");
        await File.WriteAllTextAsync(currentBookPath, "current book");
        await File.WriteAllTextAsync(currentCoverPath, "current cover");

        var book = new Book
        {
            FilePath = currentBookPath,
            CoverPath = "covers/current.jpg",
            ContentHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            Format = BookFormat.Epub,
            Title = "当前书",
            FileSize = currentBookPath.Length,
            FileModifiedUtc = DateTimeOffset.UtcNow,
        };
        await repository.InsertAsync(book);

        var orphanBookPath = Path.Combine(paths.BooksDirectory, "orphan.epub");
        var orphanCoverPath = Path.Combine(paths.CoversDirectory, "orphan.jpg");
        var cachePath = Path.Combine(paths.EpubCacheDirectory, book.ContentHash, "chapter.xhtml");
        var logPath = Path.Combine(paths.LogsDirectory, "app.log");
        var webViewPath = Path.Combine(paths.WebView2Directory, "EBWebView", "cache.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(webViewPath)!);
        await File.WriteAllTextAsync(orphanBookPath, "orphan book");
        await File.WriteAllTextAsync(orphanCoverPath, "orphan cover");
        await File.WriteAllTextAsync(cachePath, "cache");
        await File.WriteAllTextAsync(logPath, "log");
        await File.WriteAllTextAsync(webViewPath, "webview cache");

        var slimming = new LibrarySlimmingService(paths, repository);
        var report = await slimming.ScanAsync();

        Assert.True(report.ImmediateBytes > 0);
        Assert.True(report.DeferredBytes > 0);
        Assert.Equal(1, report.Targets.Single(target => target.Key == "orphan-books").FileCount);
        Assert.Equal(1, report.Targets.Single(target => target.Key == "orphan-covers").FileCount);

        var result = await slimming.CleanAsync();

        Assert.True(result.RemovedBytes > 0);
        Assert.True(File.Exists(currentBookPath));
        Assert.True(File.Exists(currentCoverPath));
        Assert.False(File.Exists(orphanBookPath));
        Assert.False(File.Exists(orphanCoverPath));
        Assert.False(File.Exists(cachePath));
        Assert.False(File.Exists(logPath));
        Assert.True(File.Exists(webViewPath));
        Assert.True(File.Exists(paths.DeferredWebViewCleanupMarker));
        Assert.True(result.ScheduledBytes > 0);

        Assert.True(await slimming.ApplyDeferredWebViewCleanupAsync());
        Assert.False(Directory.Exists(paths.WebView2Directory));
        Assert.False(File.Exists(paths.DeferredWebViewCleanupMarker));
    }
}
