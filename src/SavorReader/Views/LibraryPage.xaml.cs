using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using SavorReader.Core.Models;
using SavorReader.Services;
using SavorReader.ViewModels;

namespace SavorReader.Views;

public sealed partial class LibraryPage : Page
{
    private bool _suppressStatusChange;

    public LibraryPage()
    {
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    public LibraryViewModel ViewModel { get; } = new();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        AppLog.Info("LibraryPage navigated to.");
        await ViewModel.RefreshAsync();
        AppLog.Info("LibraryPage refresh complete.");
        UpdateViewMode();
        UpdateStatusCombo();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LibraryViewModel.SelectedBook) or "" or null
            || e.PropertyName == nameof(LibraryViewModel.HasSelection))
        {
            UpdateStatusCombo();
        }
    }

    private void UpdateViewMode()
    {
        BooksGrid.Visibility = ViewModel.IsGridView ? Visibility.Visible : Visibility.Collapsed;
        BooksList.Visibility = ViewModel.IsGridView ? Visibility.Collapsed : Visibility.Visible;
        ViewButton.Content = ViewModel.IsGridView ? "切换列表" : "切换网格";
    }

    private void UpdateStatusCombo()
    {
        _suppressStatusChange = true;
        StatusCombo.SelectedIndex = ViewModel.SelectedBook?.Book.Status switch
        {
            ReadingStatus.Reading => 1,
            ReadingStatus.Finished => 2,
            _ => 0,
        };
        _suppressStatusChange = false;
    }

    private async void ViewToggle_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SetViewModeAsync(!ViewModel.IsGridView);
        UpdateViewMode();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SearchText = SearchBox.Text;
    }

    private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StatusFilterBox.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        ViewModel.StatusFilter = tag switch
        {
            "unread" => ReadingStatus.Unread,
            "reading" => ReadingStatus.Reading,
            "finished" => ReadingStatus.Finished,
            _ => null,
        };
    }

    private void TagFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.TagFilter = TagFilterBox.SelectedItem as string;
    }

    private void SortFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortFilterBox.SelectedItem is ComboBoxItem { Tag: string tag })
        {
            ViewModel.SortMode = tag;
        }
    }

    private void BooksGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) =>
        OpenSelectedBook();

    private void BooksList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) =>
        OpenSelectedBook();

    private void OpenBook_Click(object sender, RoutedEventArgs e) => OpenSelectedBook();

    private void OpenSelectedBook()
    {
        if (ViewModel.SelectedBook is not { } item)
        {
            return;
        }

        if (item.IsMissing)
        {
            _ = ShowMessageAsync("文件不存在", "原始书籍文件已经不在原来的路径。可以重新导入，或把文件放回原位置。");
            return;
        }

        Frame.Navigate(
            item.IsPdf ? typeof(PdfReaderPage) : typeof(EpubReaderPage),
            item.Id);
    }

    private void OpenExternal_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedBook is not { } item)
        {
            return;
        }

        try
        {
            App.Services.Shell.OpenWithDefaultApp(item.Book.FilePath);
        }
        catch (Exception exception)
        {
            AppLog.Error("用其他应用打开失败", exception);
            _ = ShowMessageAsync("无法打开", exception.Message);
        }
    }

    private void RevealFile_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedBook is not { } item)
        {
            return;
        }

        try
        {
            App.Services.Shell.RevealInExplorer(item.Book.FilePath);
        }
        catch (Exception exception)
        {
            AppLog.Error("在资源管理器中显示失败", exception);
            _ = ShowMessageAsync("无法定位文件", exception.Message);
        }
    }

    private async void RemoveBook_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedBook is not { } item)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "从书库移除",
            Content = $"确定移除《{item.Title}》吗？\n\n只会删除书库记录和缓存，不会删除原始文件。",
            PrimaryButtonText = "移除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.RemoveSelectedBookAsync();
            UpdateStatusCombo();
        }
    }

    private async void AddTag_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.AddTagAsync(NewTagBox.Text);
        NewTagBox.Text = string.Empty;
    }

    private async void RemoveTag_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
        {
            await ViewModel.RemoveTagAsync(tag);
        }
    }

    private async void StatusCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressStatusChange
            || StatusCombo.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        var status = tag switch
        {
            "reading" => ReadingStatus.Reading,
            "finished" => ReadingStatus.Finished,
            _ => ReadingStatus.Unread,
        };

        await ViewModel.SetStatusAsync(status);
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "知道了",
        };
        await dialog.ShowAsync();
    }
}
