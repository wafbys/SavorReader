using Microsoft.UI.Xaml;

namespace SavorReader.ViewModels;

public sealed record TocItemViewModel(
    string Title,
    Thickness Indent,
    int SpineIndex,
    string? Anchor);
