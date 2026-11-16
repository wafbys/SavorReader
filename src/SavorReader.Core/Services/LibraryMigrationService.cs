using SavorReader.Core.Models;

namespace SavorReader.Core.Services;

/// <summary>
/// One-time maintenance for libraries created before books were copied into Data\Books.
/// Moves each external file into the managed Books directory and updates the database.
/// </summary>
public sealed class LibraryMigrationService
{
    private readonly AppPaths _paths;
    private readonly BookRepository _books;

    public LibraryMigrationService(AppPaths paths, BookRepository books)
    {
        _paths = paths;
        _books = books;
    }

    public async Task<int> EnsureManagedBooksAsync(CancellationToken cancellationToken = default)
    {
        var books = await _books.GetAllAsync(cancellationToken);
        var moved = 0;

        foreach (var book in books)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var managed = _paths.IsManagedBookPath(book.FilePath);
            var needsRename = managed && ManagedBookFiles.LooksLikeHashName(book.FilePath);
            if (managed && !needsRename)
            {
                continue;
            }

            if (!managed && !File.Exists(book.FilePath))
            {
                continue;
            }

            try
            {
                var hash = string.IsNullOrWhiteSpace(book.ContentHash)
                    ? await FileHashService.ComputeSha256Async(book.FilePath, cancellationToken)
                    : book.ContentHash;
                var managedPath = ManagedBookFiles.Allocate(
                    _paths.BooksDirectory,
                    ManagedBookFiles.DesiredName(book, managed),
                    excludePath: book.FilePath,
                    sourcePath: book.SourcePath);

                if (needsRename && !string.Equals(
                        Path.GetFullPath(managedPath),
                        Path.GetFullPath(book.FilePath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    File.Move(book.FilePath, managedPath);
                }
                else if (!managed)
                {
                    await BookImportService.EnsureManagedCopyAsync(
                        book.FilePath,
                        managedPath,
                        cancellationToken);
                    book.SourcePath ??= book.FilePath;
                }

                book.FilePath = managedPath;
                book.ContentHash = hash;
                await _books.UpdateAsync(book, cancellationToken);
                moved++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Keep the old path usable if copying or renaming one book fails.
            }
        }

        return moved;
    }
}
