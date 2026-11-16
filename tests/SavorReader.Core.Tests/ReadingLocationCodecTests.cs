using SavorReader.Core.Models;

namespace SavorReader.Core.Tests;

public sealed class ReadingLocationCodecTests
{
    [Fact]
    public void RoundTripsEpubLocation()
    {
        var json = ReadingLocationCodec.ToJson(new EpubLocation(4, 0.25));

        var location = ReadingLocationCodec.FromJson(json);

        Assert.Equal(new EpubLocation(4, 0.25), location);
    }

    [Fact]
    public void InvalidJsonReturnsNull()
    {
        Assert.Null(ReadingLocationCodec.FromJson("not-json"));
        Assert.Null(ReadingLocationCodec.FromJson(null));
    }

    [Fact]
    public void NormalizesProgress()
    {
        Assert.Equal(0, ReadingLocationCodec.NormalizeProgress(double.NaN));
        Assert.Equal(1, ReadingLocationCodec.NormalizeProgress(2));
        Assert.Equal(0.5, ReadingLocationCodec.NormalizeProgress(0.5));
    }
}
