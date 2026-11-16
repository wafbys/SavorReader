using SavorReader.Core.Models;
using SavorReader.Core.Services;

namespace SavorReader.Core.Tests;

public sealed class LibraryDatabaseTests
{
    [Fact]
    public async Task Initialize_CreatesSchema_AndIsIdempotent()
    {
        using var temp = new TempDirectory();
        var database = new LibraryDatabase(Path.Combine(temp.Path, "library.db"));

        await database.InitializeAsync();
        await database.InitializeAsync();

        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";
        await using var reader = await command.ExecuteReaderAsync();
        var tables = new List<string>();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.Contains("Books", tables);
        Assert.Contains("Tags", tables);
        Assert.Contains("BookTags", tables);
        Assert.Contains("Annotations", tables);
        Assert.Contains("Settings", tables);
    }

    [Fact]
    public async Task Initialize_AddsManagedSourcePathColumn()
    {
        using var temp = new TempDirectory();
        var database = new LibraryDatabase(Path.Combine(temp.Path, "library.db"));
        await database.InitializeAsync();

        await using var connection = await database.OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Books);";
        using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        Assert.Contains("FilePath", columns);
        Assert.Contains("SourcePath", columns);
    }

    [Fact]
    public async Task BookRepository_RoundTripsBookTagsAndProgress()
    {
        using var temp = new TempDirectory();
        var database = new LibraryDatabase(Path.Combine(temp.Path, "library.db"));
        await database.InitializeAsync();
        var repository = new BookRepository(database);
        var book = new Book
        {
            FilePath = Path.Combine(temp.Path, "sample.epub"),
            Format = BookFormat.Epub,
            Title = "测试书",
            Author = "测试作者",
            FileSize = 100,
            FileModifiedUtc = DateTimeOffset.UtcNow,
            AddedAtUtc = DateTimeOffset.UtcNow,
        };

        var id = await repository.InsertAsync(book);
        Assert.True(id > 0);
        await repository.SetTagsAsync(id, ["文学", "测试", "文学"]);

        var loaded = await repository.GetByIdAsync(id);
        Assert.NotNull(loaded);
        Assert.Equal("测试书", loaded.Title);
        Assert.Equal(2, loaded.Tags.Count);
        Assert.Contains("文学", loaded.Tags);
        Assert.Contains("测试", loaded.Tags);

        await repository.UpdateProgressAsync(
            id,
            ReadingStatus.Reading,
            0.42,
            new EpubLocation(3, 0.5),
            DateTimeOffset.UtcNow);

        var progressed = await repository.GetByIdAsync(id);
        Assert.NotNull(progressed);
        Assert.Equal(ReadingStatus.Reading, progressed.Status);
        Assert.Equal(0.42, progressed.Progress, 3);
        Assert.Equal(new EpubLocation(3, 0.5), progressed.Location);

        await repository.DeleteAsync(id);
        Assert.Null(await repository.GetByIdAsync(id));
    }

    [Fact]
    public async Task BookRepository_StoresManagedPathRelativeToDataRoot()
    {
        using var temp = new TempDirectory();
        var dataRoot = Path.Combine(temp.Path, "data");
        var database = new LibraryDatabase(Path.Combine(dataRoot, "library.db"));
        await database.InitializeAsync();
        var repository = new BookRepository(database);
        var managedPath = Path.Combine(dataRoot, "Books", "name.epub");
        Directory.CreateDirectory(Path.GetDirectoryName(managedPath)!);
        await File.WriteAllTextAsync(managedPath, "fixture");

        var book = new Book
        {
            FilePath = managedPath,
            Format = BookFormat.Epub,
            Title = "相对路径测试",
            FileSize = 7,
            FileModifiedUtc = DateTimeOffset.UtcNow,
        };
        await repository.InsertAsync(book);

        string storedPath;
        await using (var connection = await database.OpenConnectionAsync())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT FilePath FROM Books WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", book.Id);
            storedPath = (string)(await command.ExecuteScalarAsync())!;
        }

        Assert.Equal("Books/name.epub", storedPath);

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var movedRoot = Path.Combine(temp.Path, "moved");
        Directory.Move(dataRoot, movedRoot);
        var movedDatabase = new LibraryDatabase(Path.Combine(movedRoot, "library.db"));
        await movedDatabase.InitializeAsync();
        var movedRepository = new BookRepository(movedDatabase);
        var loaded = await movedRepository.GetByIdAsync(book.Id);

        Assert.NotNull(loaded);
        Assert.Equal(Path.Combine(movedRoot, "Books", "name.epub"), loaded.FilePath);
    }

    [Fact]
    public async Task Initialize_NormalizesV2AbsoluteManagedPaths()
    {
        using var temp = new TempDirectory();
        var dataRoot = Path.Combine(temp.Path, "data");
        var database = new LibraryDatabase(Path.Combine(dataRoot, "library.db"));
        await database.InitializeAsync();
        var repository = new BookRepository(database);
        var managedPath = Path.Combine(dataRoot, "Books", "v2.epub");
        Directory.CreateDirectory(Path.GetDirectoryName(managedPath)!);
        await File.WriteAllTextAsync(managedPath, "v2");

        var book = new Book
        {
            FilePath = managedPath,
            Format = BookFormat.Epub,
            Title = "v2 迁移测试",
            FileSize = 2,
            FileModifiedUtc = DateTimeOffset.UtcNow,
        };
        await repository.InsertAsync(book);

        await using (var connection = await database.OpenConnectionAsync())
        using (var update = connection.CreateCommand())
        {
            update.CommandText = """
                UPDATE Books SET FilePath = $path WHERE Id = $id;
                PRAGMA user_version=2;
                """;
            update.Parameters.AddWithValue("$path", managedPath);
            update.Parameters.AddWithValue("$id", book.Id);
            await update.ExecuteNonQueryAsync();
        }

        await database.InitializeAsync();

        await using (var connection = await database.OpenConnectionAsync())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT FilePath FROM Books WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", book.Id);
            Assert.Equal("Books/v2.epub", (string?)await command.ExecuteScalarAsync());
        }
    }

    [Fact]
    public async Task ReadingProgressService_MarksFinishedAtTheEnd()
    {
        using var temp = new TempDirectory();
        var database = new LibraryDatabase(Path.Combine(temp.Path, "library.db"));
        await database.InitializeAsync();
        var repository = new BookRepository(database);
        var book = new Book
        {
            FilePath = Path.Combine(temp.Path, "book.epub"),
            Format = BookFormat.Epub,
            Title = "进度测试",
            FileSize = 10,
            FileModifiedUtc = DateTimeOffset.UtcNow,
        };
        await repository.InsertAsync(book);
        var progress = new ReadingProgressService(repository);

        await progress.MarkOpenedAsync(book);
        await progress.SaveEpubProgressAsync(book, 2, 0.9, 1.0);

        var loaded = await repository.GetByIdAsync(book.Id);
        Assert.NotNull(loaded);
        Assert.Equal(ReadingStatus.Finished, loaded.Status);
        Assert.Equal(1.0, loaded.Progress);
        Assert.Equal(new EpubLocation(2, 0.9), loaded.Location);
    }
}
