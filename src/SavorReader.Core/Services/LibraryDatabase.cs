using Microsoft.Data.Sqlite;

namespace SavorReader.Core.Services;

public sealed class LibraryDatabase
{
    public const int SchemaVersion = 1;

    public LibraryDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = Path.GetFullPath(databasePath);
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
    }

    public string DatabasePath { get; }

    public string ConnectionString { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, """
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=NORMAL;
            PRAGMA foreign_keys=ON;
            PRAGMA busy_timeout=5000;
            """, cancellationToken);

        var version = await ScalarLongAsync(connection, "PRAGMA user_version;", cancellationToken);
        if (version > SchemaVersion)
        {
            throw new InvalidOperationException(
                $"Library database schema version {version} is newer than this app supports ({SchemaVersion}).");
        }

        if (version == 0)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await ExecuteAsync(connection, """
                CREATE TABLE IF NOT EXISTS Books (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FilePath TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    Format TEXT NOT NULL CHECK (Format IN ('epub', 'pdf')),
                    Title TEXT NOT NULL,
                    Author TEXT NULL,
                    Publisher TEXT NULL,
                    Language TEXT NULL,
                    Description TEXT NULL,
                    CoverPath TEXT NULL,
                    PageCount INTEGER NULL,
                    FileSize INTEGER NOT NULL,
                    FileModifiedUtc TEXT NOT NULL,
                    ContentHash TEXT NULL,
                    AddedAtUtc TEXT NOT NULL,
                    LastOpenedAtUtc TEXT NULL,
                    Status TEXT NOT NULL DEFAULT 'unread'
                        CHECK (Status IN ('unread', 'reading', 'finished')),
                    Progress REAL NOT NULL DEFAULT 0
                        CHECK (Progress >= 0 AND Progress <= 1),
                    LocationJson TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS Tags (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL COLLATE NOCASE UNIQUE
                );

                CREATE TABLE IF NOT EXISTS BookTags (
                    BookId INTEGER NOT NULL REFERENCES Books(Id) ON DELETE CASCADE,
                    TagId INTEGER NOT NULL REFERENCES Tags(Id) ON DELETE CASCADE,
                    PRIMARY KEY (BookId, TagId)
                );

                CREATE TABLE IF NOT EXISTS Annotations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    BookId INTEGER NOT NULL REFERENCES Books(Id) ON DELETE CASCADE,
                    Kind TEXT NOT NULL,
                    LocationJson TEXT NOT NULL,
                    SelectedText TEXT NULL,
                    Note TEXT NULL,
                    Color TEXT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Settings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_Books_LastOpenedAtUtc
                    ON Books(LastOpenedAtUtc DESC);
                CREATE INDEX IF NOT EXISTS IX_Books_Status
                    ON Books(Status);
                CREATE INDEX IF NOT EXISTS IX_BookTags_TagId
                    ON BookTags(TagId);
                CREATE INDEX IF NOT EXISTS IX_Annotations_BookId
                    ON Annotations(BookId);

                PRAGMA user_version=1;
                """, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        await ExecuteAsync(connection, "PRAGMA optimize;", cancellationToken);
    }

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await ExecuteAsync(connection, """
                PRAGMA foreign_keys=ON;
                PRAGMA busy_timeout=5000;
                """, cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> ScalarLongAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
