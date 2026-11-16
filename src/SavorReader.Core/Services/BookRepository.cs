using System.Globalization;
using Microsoft.Data.Sqlite;
using SavorReader.Core.Models;

namespace SavorReader.Core.Services;

public sealed class BookRepository
{
    private const string SelectColumns = """
        Id, FilePath, SourcePath, Format, Title, Author, Publisher, Language, Description,
        CoverPath, PageCount, FileSize, FileModifiedUtc, ContentHash, AddedAtUtc,
        LastOpenedAtUtc, Status, Progress, LocationJson
        """;

    private readonly LibraryDatabase _database;

    public BookRepository(LibraryDatabase database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {SelectColumns}
            FROM Books
            ORDER BY COALESCE(LastOpenedAtUtc, AddedAtUtc) DESC, Id DESC;
            """;

        var books = new List<Book>();
        using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                books.Add(ReadBook(reader));
            }
        }

        await LoadTagsAsync(connection, books, cancellationToken);
        return books;
    }

    public async Task<Book?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        var book = await ReadSingleAsync(connection, "WHERE Id = $id", ("$id", id), cancellationToken);
        if (book is not null)
        {
            await LoadTagsAsync(connection, [book], cancellationToken);
        }

        return book;
    }

    public async Task<Book?> GetByPathAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        var normalized = Path.GetFullPath(filePath);
        return await ReadSingleAsync(
            connection,
            "WHERE FilePath = $path COLLATE NOCASE",
            ("$path", normalized),
            cancellationToken);
    }

    public async Task<Book?> GetBySourcePathAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        var normalized = Path.GetFullPath(sourcePath);
        return await ReadSingleAsync(
            connection,
            "WHERE SourcePath = $path COLLATE NOCASE",
            ("$path", normalized),
            cancellationToken);
    }

    public async Task<Book?> GetByContentHashAsync(
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        return await ReadSingleAsync(
            connection,
            "WHERE ContentHash = $hash COLLATE NOCASE",
            ("$hash", contentHash),
            cancellationToken);
    }

    public async Task<long> InsertAsync(Book book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Books (
                FilePath, SourcePath, Format, Title, Author, Publisher, Language, Description,
                CoverPath, PageCount, FileSize, FileModifiedUtc, ContentHash,
                AddedAtUtc, LastOpenedAtUtc, Status, Progress, LocationJson)
            VALUES (
                $path, $sourcePath, $format, $title, $author, $publisher, $language, $description,
                $coverPath, $pageCount, $fileSize, $fileModified, $contentHash,
                $added, $lastOpened, $status, $progress, $location)
            RETURNING Id;
            """;
        AddBookParameters(command, book);
        var id = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        book.Id = id;
        return id;
    }

    public async Task UpdateAsync(Book book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Books SET
                FilePath = $path,
                SourcePath = $sourcePath,
                Format = $format,
                Title = $title,
                Author = $author,
                Publisher = $publisher,
                Language = $language,
                Description = $description,
                CoverPath = $coverPath,
                PageCount = $pageCount,
                FileSize = $fileSize,
                FileModifiedUtc = $fileModified,
                ContentHash = $contentHash,
                AddedAtUtc = $added,
                LastOpenedAtUtc = $lastOpened,
                Status = $status,
                Progress = $progress,
                LocationJson = $location
            WHERE Id = $id;
            """;
        AddBookParameters(command, book);
        command.Parameters.AddWithValue("$id", book.Id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateProgressAsync(
        long bookId,
        ReadingStatus status,
        double progress,
        ReadingLocation? location,
        DateTimeOffset lastOpenedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Books SET
                Status = $status,
                Progress = $progress,
                LocationJson = $location,
                LastOpenedAtUtc = $lastOpened
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$status", status.ToStorageValue());
        command.Parameters.AddWithValue("$progress", ReadingLocationCodec.NormalizeProgress(progress));
        command.Parameters.AddWithValue("$location", (object?)ReadingLocationCodec.ToJson(location) ?? DBNull.Value);
        command.Parameters.AddWithValue("$lastOpened", FormatDate(lastOpenedAtUtc));
        command.Parameters.AddWithValue("$id", bookId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkOpenedAsync(
        long bookId,
        DateTimeOffset openedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Books SET
                LastOpenedAtUtc = $lastOpened,
                Status = CASE WHEN Status = 'unread' THEN 'reading' ELSE Status END
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$lastOpened", FormatDate(openedAtUtc));
        command.Parameters.AddWithValue("$id", bookId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(
        long bookId,
        ReadingStatus status,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Books SET Status = $status WHERE Id = $id;";
        command.Parameters.AddWithValue("$status", status.ToStorageValue());
        command.Parameters.AddWithValue("$id", bookId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateCoverAsync(
        long bookId,
        string? coverPath,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Books SET CoverPath = $coverPath WHERE Id = $id;";
        command.Parameters.AddWithValue("$coverPath", (object?)coverPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", bookId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(long bookId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Books WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", bookId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetTagsAsync(long bookId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.Name
            FROM Tags t
            JOIN BookTags bt ON bt.TagId = t.Id
            WHERE bt.BookId = $id
            ORDER BY t.Name COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$id", bookId);

        var tags = new List<string>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tags.Add(reader.GetString(0));
        }

        return tags;
    }

    public async Task<IReadOnlyList<string>> GetAllTagsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM Tags ORDER BY Name COLLATE NOCASE;";

        var tags = new List<string>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tags.Add(reader.GetString(0));
        }

        return tags;
    }

    public async Task SetTagsAsync(
        long bookId,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default)
    {
        var normalized = tags
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = (SqliteTransaction)transaction;
            delete.CommandText = "DELETE FROM BookTags WHERE BookId = $id;";
            delete.Parameters.AddWithValue("$id", bookId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var tag in normalized)
        {
            using var upsert = connection.CreateCommand();
            upsert.Transaction = (SqliteTransaction)transaction;
            upsert.CommandText = """
                INSERT INTO Tags(Name) VALUES($name)
                ON CONFLICT(Name) DO NOTHING;
                INSERT INTO BookTags(BookId, TagId)
                SELECT $id, Id FROM Tags WHERE Name = $name COLLATE NOCASE;
                """;
            upsert.Parameters.AddWithValue("$name", tag);
            upsert.Parameters.AddWithValue("$id", bookId);
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> AnyOtherBookUsesCoverAsync(
        string coverPath,
        long excludingBookId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS(
                SELECT 1 FROM Books
                WHERE CoverPath = $coverPath COLLATE NOCASE AND Id <> $id
            );
            """;
        command.Parameters.AddWithValue("$coverPath", coverPath);
        command.Parameters.AddWithValue("$id", excludingBookId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) != 0;
    }

    public async Task<bool> AnyOtherBookUsesFilePathAsync(
        string filePath,
        long excludingBookId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS(
                SELECT 1 FROM Books
                WHERE FilePath = $filePath COLLATE NOCASE AND Id <> $id
            );
            """;
        command.Parameters.AddWithValue("$filePath", filePath);
        command.Parameters.AddWithValue("$id", excludingBookId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) != 0;
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Books;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task<Book?> ReadSingleAsync(
        SqliteConnection connection,
        string whereClause,
        (string Name, object Value) parameter,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM Books {whereClause} LIMIT 1;";
        command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadBook(reader) : null;
    }

    private static async Task LoadTagsAsync(
        SqliteConnection connection,
        IReadOnlyCollection<Book> books,
        CancellationToken cancellationToken)
    {
        if (books.Count == 0)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT bt.BookId, t.Name
            FROM BookTags bt
            JOIN Tags t ON t.Id = bt.TagId
            ORDER BY t.Name COLLATE NOCASE;
            """;

        var byBook = books.ToDictionary(book => book.Id);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (byBook.TryGetValue(reader.GetInt64(0), out var book))
            {
                book.Tags.Add(reader.GetString(1));
            }
        }
    }

    private static Book ReadBook(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        FilePath = reader.GetString(1),
        SourcePath = reader.IsDBNull(2) ? null : reader.GetString(2),
        Format = BookFormatExtensions.ParseStorageValue(reader.GetString(3)),
        Title = reader.GetString(4),
        Author = reader.IsDBNull(5) ? null : reader.GetString(5),
        Publisher = reader.IsDBNull(6) ? null : reader.GetString(6),
        Language = reader.IsDBNull(7) ? null : reader.GetString(7),
        Description = reader.IsDBNull(8) ? null : reader.GetString(8),
        CoverPath = reader.IsDBNull(9) ? null : reader.GetString(9),
        PageCount = reader.IsDBNull(10) ? null : reader.GetInt32(10),
        FileSize = reader.GetInt64(11),
        FileModifiedUtc = ParseDate(reader.GetString(12)),
        ContentHash = reader.IsDBNull(13) ? null : reader.GetString(13),
        AddedAtUtc = ParseDate(reader.GetString(14)),
        LastOpenedAtUtc = reader.IsDBNull(15) ? null : ParseDate(reader.GetString(15)),
        Status = ReadingStatusExtensions.ParseStorageValue(reader.GetString(16)),
        Progress = reader.GetDouble(17),
        Location = reader.IsDBNull(18) ? null : ReadingLocationCodec.FromJson(reader.GetString(18)),
    };

    private static void AddBookParameters(SqliteCommand command, Book book)
    {
        command.Parameters.AddWithValue("$path", Path.GetFullPath(book.FilePath));
        command.Parameters.AddWithValue("$sourcePath", (object?)book.SourcePath ?? DBNull.Value);
        command.Parameters.AddWithValue("$format", book.Format.ToStorageValue());
        command.Parameters.AddWithValue("$title", book.Title);
        command.Parameters.AddWithValue("$author", (object?)book.Author ?? DBNull.Value);
        command.Parameters.AddWithValue("$publisher", (object?)book.Publisher ?? DBNull.Value);
        command.Parameters.AddWithValue("$language", (object?)book.Language ?? DBNull.Value);
        command.Parameters.AddWithValue("$description", (object?)book.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$coverPath", (object?)book.CoverPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$pageCount", (object?)book.PageCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$fileSize", book.FileSize);
        command.Parameters.AddWithValue("$fileModified", FormatDate(book.FileModifiedUtc));
        command.Parameters.AddWithValue("$contentHash", (object?)book.ContentHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$added", FormatDate(book.AddedAtUtc));
        command.Parameters.AddWithValue("$lastOpened", book.LastOpenedAtUtc is { } last ? FormatDate(last) : DBNull.Value);
        command.Parameters.AddWithValue("$status", book.Status.ToStorageValue());
        command.Parameters.AddWithValue("$progress", ReadingLocationCodec.NormalizeProgress(book.Progress));
        command.Parameters.AddWithValue("$location", (object?)ReadingLocationCodec.ToJson(book.Location) ?? DBNull.Value);
    }

    private static string FormatDate(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
