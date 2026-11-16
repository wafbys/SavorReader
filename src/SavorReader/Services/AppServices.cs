using SavorReader.Core.Services;

namespace SavorReader.Services;

public sealed class AppServices
{
    private AppServices(
        AppPaths paths,
        LibraryDatabase database,
        BookRepository books,
        SettingsService settings,
        EpubBookReader epubReader,
        EpubCacheService epubCache,
        BookImportService imports,
        ReadingProgressService progress,
        PdfCoverService pdfCovers,
        EpubReaderAssets epubReaderAssets,
        LibrarySlimmingService slimming,
        WebView2EnvironmentProvider webView2,
        ShellService shell,
        FilePickerService pickers)
    {
        Paths = paths;
        Database = database;
        Books = books;
        Settings = settings;
        EpubReader = epubReader;
        EpubCache = epubCache;
        Imports = imports;
        Progress = progress;
        PdfCovers = pdfCovers;
        EpubReaderAssets = epubReaderAssets;
        Slimming = slimming;
        WebView2 = webView2;
        Shell = shell;
        Pickers = pickers;
    }

    public AppPaths Paths { get; }

    public LibraryDatabase Database { get; }

    public BookRepository Books { get; }

    public SettingsService Settings { get; }

    public EpubBookReader EpubReader { get; }

    public EpubCacheService EpubCache { get; }

    public BookImportService Imports { get; }

    public ReadingProgressService Progress { get; }

    public PdfCoverService PdfCovers { get; }

    public EpubReaderAssets EpubReaderAssets { get; }

    public LibrarySlimmingService Slimming { get; }

    public WebView2EnvironmentProvider WebView2 { get; }

    public ShellService Shell { get; }

    public FilePickerService Pickers { get; }

    public static async Task<AppServices> CreateAsync(CancellationToken cancellationToken = default)
    {
        var paths = AppPaths.CreateDefault();
        var database = new LibraryDatabase(paths.DatabasePath);
        await database.InitializeAsync(cancellationToken);

        var books = new BookRepository(database);
        var epubReader = new EpubBookReader();
        var pdfReader = new PdfMetadataReader();
        var epubCache = new EpubCacheService(paths);

        try
        {
            var moved = await new LibraryMigrationService(paths, books)
                .EnsureManagedBooksAsync(cancellationToken);
            if (moved > 0)
            {
                AppLog.Info($"已将 {moved} 本旧书籍整理到 {paths.BooksDirectory}");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AppLog.Warn("整理旧书籍副本失败，原路径仍可继续使用。", exception);
        }

        var slimming = new LibrarySlimmingService(paths, books);
        try
        {
            if (await slimming.ApplyDeferredWebViewCleanupAsync(cancellationToken))
            {
                AppLog.Info("已按计划清理 WebView2 浏览器缓存。");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AppLog.Warn("清理 WebView2 浏览器缓存失败，将在下次启动重试。", exception);
        }

        return new AppServices(
            paths,
            database,
            books,
            new SettingsService(database),
            epubReader,
            epubCache,
            new BookImportService(paths, books, epubReader, pdfReader),
            new ReadingProgressService(books),
            new PdfCoverService(paths, books),
            new EpubReaderAssets(paths, epubCache),
            slimming,
            new WebView2EnvironmentProvider(paths),
            new ShellService(),
            new FilePickerService());
    }
}
