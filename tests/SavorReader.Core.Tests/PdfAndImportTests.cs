using System.IO.Compression;
using SavorReader.Core.Models;
using SavorReader.Core.Services;

namespace SavorReader.Core.Tests;

public sealed class PdfAndImportTests
{
    [Fact]
    public void PdfMetadataReader_ReadsTitleAuthorAndPageCount()
    {
        using var temp = new TempDirectory();
        var path = BookFixtures.CreatePdf(Path.Combine(temp.Path, "book.pdf"));

        var metadata = new PdfMetadataReader().Read(path);

        Assert.Equal("Test PDF", metadata.Title);
        Assert.Equal("Test Author", metadata.Author);
        Assert.Equal(1, metadata.PageCount);
    }

    [Fact]
    public async Task BookImportService_ImportsAndDeduplicatesFiles()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(Path.Combine(temp.Path, "data"));
        var database = new LibraryDatabase(paths.DatabasePath);
        await database.InitializeAsync();
        var repository = new BookRepository(database);
        var imports = new BookImportService(
            paths,
            repository,
            new EpubBookReader(),
            new PdfMetadataReader());

        var epubPath = BookFixtures.CreateEpub(Path.Combine(temp.Path, "book.epub"));
        var pdfPath = BookFixtures.CreatePdf(Path.Combine(temp.Path, "book.pdf"));

        var epubResult = await imports.ImportFileAsync(epubPath);
        var epubBook = epubResult switch
        {
            ImportSuccess success => success.Book,
            ImportDuplicate => throw new Xunit.Sdk.XunitException("EPUB was imported twice unexpectedly."),
            ImportFailure failure => throw new Xunit.Sdk.XunitException(failure.Message),
        };
        Assert.Equal(BookFormat.Epub, epubBook.Format);
        Assert.Equal(epubPath, epubBook.SourcePath);
        Assert.StartsWith(paths.BooksDirectory, epubBook.FilePath);
        Assert.Equal(Path.GetFileName(epubPath), Path.GetFileName(epubBook.FilePath));
        Assert.True(File.Exists(epubBook.FilePath));
        Assert.NotNull(epubBook.ContentHash);
        Assert.NotNull(epubBook.CoverPath);
        Assert.True(File.Exists(paths.ResolveCoverPath(epubBook.CoverPath)));

        var duplicate = await imports.ImportFileAsync(epubPath);
        Assert.True(duplicate is ImportDuplicate);

        var copyPath = Path.Combine(temp.Path, "book-copy.epub");
        File.Copy(epubPath, copyPath);
        var duplicateContent = await imports.ImportFileAsync(copyPath);
        Assert.True(duplicateContent is ImportDuplicate);

        var pdfResult = await imports.ImportFileAsync(pdfPath);
        var pdfBook = pdfResult switch
        {
            ImportSuccess success => success.Book,
            ImportDuplicate => throw new Xunit.Sdk.XunitException("PDF was imported twice unexpectedly."),
            ImportFailure failure => throw new Xunit.Sdk.XunitException(failure.Message),
        };
        Assert.Equal(BookFormat.Pdf, pdfBook.Format);
        Assert.Equal(pdfPath, pdfBook.SourcePath);
        Assert.StartsWith(paths.BooksDirectory, pdfBook.FilePath);
        Assert.Equal(Path.GetFileName(pdfPath), Path.GetFileName(pdfBook.FilePath));
        Assert.True(File.Exists(pdfBook.FilePath));
        Assert.NotNull(pdfBook.ContentHash);
        Assert.Equal(1, pdfBook.PageCount);
        Assert.Equal("Test PDF", pdfBook.Title);
    }

    [Fact]
    public async Task Import_SameFileNameGetsNumericSuffix()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(Path.Combine(temp.Path, "data"));
        var database = new LibraryDatabase(paths.DatabasePath);
        await database.InitializeAsync();
        var repository = new BookRepository(database);
        var imports = new BookImportService(
            paths,
            repository,
            new EpubBookReader(),
            new PdfMetadataReader());

        var firstPath = BookFixtures.CreateEpub(Path.Combine(temp.Path, "a", "same.epub"));
        var secondPath = BookFixtures.CreateEpub(Path.Combine(temp.Path, "b", "same.epub"));

        using (var archive = ZipFile.Open(secondPath, ZipArchiveMode.Update))
        {
            var entry = archive.CreateEntry("extra.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("different content");
        }

        var first = await imports.ImportFileAsync(firstPath);
        var second = await imports.ImportFileAsync(secondPath);

        var firstBook = first switch
        {
            ImportSuccess success => success.Book,
            _ => throw new Xunit.Sdk.XunitException("First import failed."),
        };
        var secondBook = second switch
        {
            ImportSuccess success => success.Book,
            _ => throw new Xunit.Sdk.XunitException("Second import failed."),
        };

        Assert.Equal("same.epub", Path.GetFileName(firstBook.FilePath));
        Assert.Equal("same (2).epub", Path.GetFileName(secondBook.FilePath));
    }
}
