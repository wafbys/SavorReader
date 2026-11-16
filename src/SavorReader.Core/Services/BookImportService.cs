using SavorReader.Core.Models;

namespace SavorReader.Core.Services;

public sealed class BookImportService
{
    private readonly AppPaths _paths;
    private readonly BookRepository _books;
    private readonly EpubBookReader _epubReader;
    private readonly PdfMetadataReader _pdfReader;

    public BookImportService(
        AppPaths paths,
        BookRepository books,
        EpubBookReader epubReader,
        PdfMetadataReader pdfReader)
    {
        _paths = paths;
        _books = books;
        _epubReader = epubReader;
        _pdfReader = pdfReader;
    }

    public async Task<ImportOutcome> ImportFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = string.Empty;
        try
        {
            fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath))
            {
                return new ImportFailure(fullPath, "文件不存在。");
            }

            var format = BookFormatExtensions.FromPath(fullPath);
            if (format is null)
            {
                return new ImportFailure(fullPath, "只支持 .epub 和 .pdf 文件。");
            }

            var existing = await _books.GetByPathAsync(fullPath, cancellationToken);
            if (existing is not null)
            {
                return new ImportDuplicate(existing);
            }

            var fileInfo = new FileInfo(fullPath);
            var contentHash = format == BookFormat.Epub
                ? await FileHashService.ComputeSha256Async(fullPath, cancellationToken)
                : null;

            var metadata = await Task.Run(
                () => format == BookFormat.Epub
                    ? _epubReader.Read(fullPath).Metadata
                    : _pdfReader.Read(fullPath),
                cancellationToken);

            var coverPath = await SaveCoverAsync(metadata, contentHash, cancellationToken);
            var book = new Book
            {
                FilePath = fullPath,
                Format = format.Value,
                Title = string.IsNullOrWhiteSpace(metadata.Title)
                    ? Path.GetFileNameWithoutExtension(fullPath)
                    : metadata.Title,
                Author = metadata.Author,
                Publisher = metadata.Publisher,
                Language = metadata.Language,
                Description = metadata.Description,
                CoverPath = coverPath,
                PageCount = metadata.PageCount,
                FileSize = fileInfo.Length,
                FileModifiedUtc = new DateTimeOffset(fileInfo.LastWriteTimeUtc),
                ContentHash = contentHash,
                AddedAtUtc = DateTimeOffset.UtcNow,
                Status = ReadingStatus.Unread,
                Progress = 0,
            };

            await _books.InsertAsync(book, cancellationToken);
            return new ImportSuccess(book);
        }
        catch (Exception exception)
        {
            return new ImportFailure(
                fullPath.Length == 0 ? filePath : fullPath,
                exception.Message);
        }
    }

    public async Task<IReadOnlyList<ImportOutcome>> ImportManyAsync(
        IEnumerable<string> filePaths,
        IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var paths = filePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var results = new List<ImportOutcome>(paths.Length);
        for (var index = 0; index < paths.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ImportProgress(index, paths.Length, paths[index]));
            results.Add(await ImportFileAsync(paths[index], cancellationToken));
        }

        progress?.Report(new ImportProgress(paths.Length, paths.Length, string.Empty));
        return results;
    }

    public static IReadOnlyList<string> EnumerateBooks(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        };

        return Directory
            .EnumerateFiles(folder, "*", options)
            .Where(path => BookFormatExtensions.FromPath(path) is not null)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<string?> SaveCoverAsync(
        BookMetadata metadata,
        string? contentHash,
        CancellationToken cancellationToken)
    {
        if (metadata.CoverBytes is not { Length: > 0 }
            || string.IsNullOrWhiteSpace(metadata.CoverExtension)
            || string.IsNullOrWhiteSpace(contentHash))
        {
            return null;
        }

        var fileName = $"{contentHash[..16]}{metadata.CoverExtension}";
        var destination = Path.Combine(_paths.CoversDirectory, fileName);
        await File.WriteAllBytesAsync(destination, metadata.CoverBytes, cancellationToken);
        return _paths.ToRelativeCoverPath(fileName);
    }
}
