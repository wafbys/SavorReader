using System.IO.Compression;
using SavorReader.Core.Services;

namespace SavorReader.Core.Tests;

public sealed class EpubTests
{
    [Fact]
    public void EpubBookReader_ReadsMetadataSpineAndToc()
    {
        using var temp = new TempDirectory();
        var path = BookFixtures.CreateEpub(Path.Combine(temp.Path, "book.epub"));

        var result = new EpubBookReader().Read(path);

        Assert.Equal("测试书", result.Metadata.Title);
        Assert.Equal("测试作者", result.Metadata.Author);
        Assert.Equal("测试出版社", result.Metadata.Publisher);
        Assert.Equal("zh-CN", result.Metadata.Language);
        Assert.Equal(2, result.Content.Spine.Count);
        Assert.Contains(result.Content.TableOfContents, item => item.Title == "第一章");
        Assert.Contains(result.Content.TableOfContents, item => item.Anchor == "start");
        Assert.NotNull(result.Metadata.CoverBytes);
        Assert.Equal(".png", result.Metadata.CoverExtension);
    }

    [Fact]
    public async Task EpubCacheService_ExtractsValidEpub()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(Path.Combine(temp.Path, "data"));
        var path = BookFixtures.CreateEpub(Path.Combine(temp.Path, "book.epub"));
        var hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        var cache = await new EpubCacheService(paths).EnsureExtractedAsync(path, hash);

        Assert.True(File.Exists(Path.Combine(cache, "META-INF", "container.xml")));
        Assert.True(File.Exists(Path.Combine(cache, "OEBPS", "content.opf")));
        Assert.True(File.Exists(Path.Combine(cache, ".ready")));
    }

    [Fact]
    public async Task EpubCacheService_RejectsZipSlip()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(Path.Combine(temp.Path, "data"));
        var path = BookFixtures.CreateEpub(
            Path.Combine(temp.Path, "unsafe.epub"),
            includeCover: false,
            addZipSlipEntry: true);
        var hash = "abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd";

        await Assert.ThrowsAsync<InvalidDataException>(
            () => new EpubCacheService(paths).EnsureExtractedAsync(path, hash));

        Assert.False(File.Exists(Path.Combine(temp.Path, "zip-slip.txt")));
    }
}
