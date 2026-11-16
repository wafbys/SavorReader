namespace SavorReader.Core.Models;

public sealed record BookMetadata(
    string Title,
    string? Author,
    string? Publisher,
    string? Language,
    string? Description,
    int? PageCount,
    byte[]? CoverBytes,
    string? CoverExtension);
