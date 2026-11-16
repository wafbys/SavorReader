namespace SavorReader.Core.Models;

public sealed record EpubSpineItem(int Index, string Href, string Title);

public sealed record EpubTocEntry(string Title, int Depth, int SpineIndex, string? Anchor);

public sealed record EpubBookContent(
    string Title,
    IReadOnlyList<EpubSpineItem> Spine,
    IReadOnlyList<EpubTocEntry> TableOfContents);

public sealed record EpubReadResult(BookMetadata Metadata, EpubBookContent Content);
