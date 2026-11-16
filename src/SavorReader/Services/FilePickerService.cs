using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SavorReader.Services;

public sealed class FilePickerService
{
    public async Task<IReadOnlyList<string>> PickBooksAsync(Window owner)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List,
        };
        picker.FileTypeFilter.Add(".epub");
        picker.FileTypeFilter.Add(".pdf");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(owner));

        var files = await picker.PickMultipleFilesAsync();
        return files is null
            ? []
            : files.Select(file => file.Path).ToArray();
    }

    public async Task<string?> PickFolderAsync(Window owner)
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(owner));

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
