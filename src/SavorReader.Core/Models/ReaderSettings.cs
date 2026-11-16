namespace SavorReader.Core.Models;

public enum ReaderTheme
{
    Day,
    Night,
}

public enum ReaderFont
{
    Serif,
    Sans,
}

public sealed record ReaderSettings
{
    public ReaderTheme Theme { get; init; } = ReaderTheme.Day;

    public ReaderFont Font { get; init; } = ReaderFont.Serif;

    public double FontSize { get; init; } = 18;

    public double LineHeight { get; init; } = 1.7;

    public double Margin { get; init; } = 48;

    public static ReaderSettings Default => new();

    public ReaderSettings Clamp() => this with
    {
        FontSize = Math.Clamp(FontSize, 12, 30),
        LineHeight = Math.Clamp(LineHeight, 1.2, 2.2),
        Margin = Math.Clamp(Margin, 16, 96),
    };

    public string ThemeName => Theme == ReaderTheme.Night ? "night" : "day";

    public string FontName => Font == ReaderFont.Serif ? "serif" : "sans";

    public string CssFontFamily => Font == ReaderFont.Serif
        ? "'Georgia', 'Noto Serif SC', 'SimSun', serif"
        : "'Segoe UI', 'Microsoft YaHei UI', sans-serif";
}
