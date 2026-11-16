using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using SavorReader.Core.Models;
using SavorReader.Services;

namespace SavorReader.Views;

public sealed partial class PdfReaderPage : Page
{
    private long _bookId;
    private Book? _book;
    private bool _webViewReady;

    public PdfReaderPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is not long bookId)
        {
            ShowError("没有收到书籍编号。");
            return;
        }

        _bookId = bookId;
        await InitializeAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (_webViewReady && ReaderWebView.CoreWebView2 is { } core)
        {
            core.NavigationCompleted -= Core_NavigationCompleted;
            core.ProcessFailed -= Core_ProcessFailed;
            core.Navigate("about:blank");
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            _book = await App.Services.Books.GetByIdAsync(_bookId);
            if (_book is null)
            {
                ShowError("这本书不在书库里。");
                return;
            }

            if (!File.Exists(_book.FilePath))
            {
                ShowError("原始 PDF 文件已经不存在。\n\n可以把文件放回原位置，或回到书库重新导入。");
                return;
            }

            TitleText.Text = _book.Title;
            AuthorText.Text = string.IsNullOrWhiteSpace(_book.Author) ? "未知作者" : _book.Author;
            PageCountText.Text = _book.PageCount is > 0 ? $"{_book.PageCount} 页" : string.Empty;

            await App.Services.Progress.MarkOpenedAsync(_book);
            await App.Services.PdfCovers.EnsureCoverAsync(_book);

            var directory = Path.GetDirectoryName(_book.FilePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                ShowError("无法确定 PDF 所在目录。");
                return;
            }

            var environment = await App.Services.WebView2.GetAsync();
            await ReaderWebView.EnsureCoreWebView2Async(environment);
            var core = ReaderWebView.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.NavigationCompleted += Core_NavigationCompleted;
            core.ProcessFailed += Core_ProcessFailed;
            core.SetVirtualHostNameToFolderMapping(
                "pdf.local",
                directory,
                CoreWebView2HostResourceAccessKind.DenyCors);

            var fileName = Uri.EscapeDataString(Path.GetFileName(_book.FilePath));
            _webViewReady = true;
            core.Navigate($"https://pdf.local/{fileName}");
        }
        catch (Exception exception)
        {
            AppLog.Error($"打开 PDF 失败：{_book?.FilePath}", exception);
            ShowError($"无法打开 PDF：\n{exception.Message}");
        }
    }

    private void Core_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            ErrorOverlay.Visibility = Visibility.Collapsed;
            AppLog.Info("PDF reader ready.");
        }
        else
        {
            ShowError($"PDF 加载失败：{e.WebErrorStatus}");
        }
    }

    private void Core_ProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        AppLog.Error($"WebView2 进程失败：{e.ProcessFailedKind}");
        ShowError("PDF 渲染进程异常退出，请返回书库后重试。");
    }

    private async void ExternalOpen_Click(object sender, RoutedEventArgs e)
    {
        if (_book is null)
        {
            return;
        }

        try
        {
            App.Services.Shell.OpenWithDefaultApp(_book.FilePath);
        }
        catch (Exception exception)
        {
            AppLog.Error("用其他应用打开 PDF 失败", exception);
            await ShowMessageAsync("无法打开", exception.Message);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            Frame.Navigate(typeof(LibraryPage));
        }
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

    private void ShowError(string message)
    {
        LoadingOverlay.Visibility = Visibility.Collapsed;
        ErrorText.Text = message;
        ErrorOverlay.Visibility = Visibility.Visible;
    }
}
