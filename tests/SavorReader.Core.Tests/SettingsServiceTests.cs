using SavorReader.Core.Models;
using SavorReader.Core.Services;

namespace SavorReader.Core.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task ReaderSettings_RoundTripAndClamp()
    {
        using var temp = new TempDirectory();
        var database = new LibraryDatabase(Path.Combine(temp.Path, "library.db"));
        await database.InitializeAsync();
        var settings = new SettingsService(database);

        await settings.SaveReaderSettingsAsync(new ReaderSettings
        {
            Theme = ReaderTheme.Night,
            Font = ReaderFont.Sans,
            FontSize = 22,
            LineHeight = 2.0,
            Margin = 32,
        });

        var loaded = await settings.GetReaderSettingsAsync();
        Assert.Equal(ReaderTheme.Night, loaded.Theme);
        Assert.Equal(ReaderFont.Sans, loaded.Font);
        Assert.Equal(22, loaded.FontSize);
        Assert.Equal(2.0, loaded.LineHeight);
        Assert.Equal(32, loaded.Margin);

        await settings.SaveReaderSettingsAsync(new ReaderSettings
        {
            FontSize = 100,
            LineHeight = 0,
            Margin = 1,
        });
        var clamped = await settings.GetReaderSettingsAsync();
        Assert.Equal(30, clamped.FontSize);
        Assert.Equal(1.2, clamped.LineHeight);
        Assert.Equal(16, clamped.Margin);
    }

    [Fact]
    public async Task LibraryViewMode_RoundTrips()
    {
        using var temp = new TempDirectory();
        var database = new LibraryDatabase(Path.Combine(temp.Path, "library.db"));
        await database.InitializeAsync();
        var settings = new SettingsService(database);

        Assert.Equal("grid", await settings.GetLibraryViewModeAsync());
        await settings.SetLibraryViewModeAsync("list");
        Assert.Equal("list", await settings.GetLibraryViewModeAsync());
    }
}
