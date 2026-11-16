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
        Assert.NotNull(epubBook.CoverPath);
        Assert.True(File.Exists(paths.ResolveCoverPath(epubBook.CoverPath)));

        var duplicate = await imports.ImportFileAsync(epubPath);
        Assert.True(duplicate is ImportDuplicate);

        var pdfResult = await imports.ImportFileAsync(pdfPath);
        var pdfBook = pdfResult switch
        {
            ImportSuccess success => success.Book,
            ImportDuplicate => throw new Xunit.Sdk.XunitException("PDF was imported twice unexpectedly."),
            ImportFailure failure => throw new Xunit.Sdk.XunitException(failure.Message),
        };
        Assert.Equal(BookFormat.Pdf, pdfBook.Format);
        Assert.Equal(1, pdfBook.PageCount);
        Assert.Equal("Test PDF", pdfBook.Title);
    }
}
