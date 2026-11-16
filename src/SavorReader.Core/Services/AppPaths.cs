namespace SavorReader.Core.Services;

public sealed class AppPaths
{
    public const string DataDirectoryEnvironmentVariable = "SAVORREADER_DATA_DIR";

    public AppPaths(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        DataRoot = Path.GetFullPath(dataRoot);
        BooksDirectory = Path.Combine(DataRoot, "Books");
        CoversDirectory = Path.Combine(DataRoot, "covers");
        EpubCacheDirectory = Path.Combine(DataRoot, "cache", "epub");
        WebView2Directory = Path.Combine(DataRoot, "webview2");
        LogsDirectory = Path.Combine(DataRoot, "logs");
        DatabasePath = Path.Combine(DataRoot, "library.db");

        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(BooksDirectory);
        Directory.CreateDirectory(CoversDirectory);
        Directory.CreateDirectory(EpubCacheDirectory);
        Directory.CreateDirectory(WebView2Directory);
        Directory.CreateDirectory(LogsDirectory);
    }

    public string DataRoot { get; }

    public string DatabasePath { get; }

    public string BooksDirectory { get; }

    public string CoversDirectory { get; }

    public string EpubCacheDirectory { get; }

    public string WebView2Directory { get; }

    public string LogsDirectory { get; }

    public string DeferredWebViewCleanupMarker => Path.Combine(DataRoot, ".slim-webview2");

    /// <summary>
    /// Data lives next to the executable by default. Set SAVORREADER_DATA_DIR to override,
    /// which is also what the test projects use.
    /// </summary>
    public static AppPaths CreateDefault()
    {
        var overridden = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);
        var root = string.IsNullOrWhiteSpace(overridden)
            ? Path.Combine(AppContext.BaseDirectory, "Data")
            : overridden;
        return new AppPaths(root);
    }

    public string GetEpubCachePath(string contentHash)
    {
        if (string.IsNullOrWhiteSpace(contentHash) || !contentHash.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException("EPUB cache key must be a hexadecimal SHA-256 hash.", nameof(contentHash));
        }

        return Path.Combine(EpubCacheDirectory, contentHash.ToLowerInvariant());
    }

    public bool IsManagedBookPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(filePath);
        var prefix = BooksDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? BooksDirectory
            : BooksDirectory + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public string ToRelativeCoverPath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Cover file name must not contain a directory separator.", nameof(fileName));
        }

        return $"covers/{fileName}";
    }

    public string? ResolveCoverPath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var candidate = Path.GetFullPath(Path.Combine(
            DataRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

        var prefix = DataRoot.EndsWith(Path.DirectorySeparatorChar)
            ? DataRoot
            : DataRoot + Path.DirectorySeparatorChar;

        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? candidate : null;
    }
}
