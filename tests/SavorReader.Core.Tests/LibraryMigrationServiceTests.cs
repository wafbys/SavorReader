using SavorReader.Core.Models;
using SavorReader.Core.Services;

namespace SavorReader.Core.Tests;

public sealed class LibraryMigrationServiceTests
{
    [Fact]
    public async Task EnsureManagedBooks_CopiesLegacyExternalBookOnce()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(Path.Combine(temp.Path, "data"));
        var database = new LibraryDatabase(paths.DatabasePath);
        await database.InitializeAsync();
        var repository = new BookRepository(database);

        var externalPath = BookFixtures.CreateEpub(Path.Combine(temp.Path, "external.epub"));
        var book = new Book
        {
            FilePath = externalPath,
            Format = BookFormat.Epub,
            Title = "旧书",
            FileSize = new FileInfo(externalPath).Length,
            FileModifiedUtc = DateTimeOffset.UtcNow,
        };
        await repository.InsertAsync(book);

        var migration = new LibraryMigrationService(paths, repository);
        var moved = await migration.EnsureManagedBooksAsync();

        Assert.Equal(1, moved);
        var loaded = await repository.GetByIdAsync(book.Id);
        Assert.NotNull(loaded);
        Assert.Equal(externalPath, loaded.SourcePath);
        Assert.StartsWith(paths.BooksDirectory, loaded.FilePath);
        Assert.Equal(Path.GetFileName(externalPath), Path.GetFileName(loaded.FilePath));
        Assert.True(File.Exists(loaded.FilePath));
        Assert.True(File.Exists(externalPath));
        Assert.NotNull(loaded.ContentHash);

        Assert.Equal(0, await migration.EnsureManagedBooksAsync());
    }

    [Fact]
    public async Task EnsureManagedBooks_RenamesHashNamedManagedCopy()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(Path.Combine(temp.Path, "data"));
        var database = new LibraryDatabase(paths.DatabasePath);
        await database.InitializeAsync();
        var repository = new BookRepository(database);

        var externalPath = BookFixtures.CreateEpub(Path.Combine(temp.Path, "原书名.epub"));
        var hash = await FileHashService.ComputeSha256Async(externalPath);
        var hashNamedPath = Path.Combine(paths.BooksDirectory, hash + ".epub");
        File.Copy(externalPath, hashNamedPath);

        var book = new Book
        {
            FilePath = hashNamedPath,
            SourcePath = externalPath,
            ContentHash = hash,
            Format = BookFormat.Epub,
            Title = "原书名",
            FileSize = new FileInfo(hashNamedPath).Length,
            FileModifiedUtc = DateTimeOffset.UtcNow,
        };
        await repository.InsertAsync(book);

        var moved = await new LibraryMigrationService(paths, repository)
            .EnsureManagedBooksAsync();

        Assert.Equal(1, moved);
        var loaded = await repository.GetByIdAsync(book.Id);
        Assert.NotNull(loaded);
        Assert.Equal(Path.Combine(paths.BooksDirectory, "原书名.epub"), loaded.FilePath);
        Assert.True(File.Exists(loaded.FilePath));
        Assert.False(File.Exists(hashNamedPath));
    }
}

