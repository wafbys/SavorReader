using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SavorReader.Services;

namespace SavorReader.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        DataPathText.Text = App.Services.Paths.DataRoot;
        AboutVersionText.Text = $"SavorReader {AppInfo.DisplayVersion} · 本地 EPUB/PDF 书库与阅读器";
        await RefreshCacheSizeAsync();
    }

    private void OpenAbout_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(AboutPage));

    private async Task RefreshCacheSizeAsync()
    {
        var bytes = await App.Services.EpubCache.GetSizeBytesAsync();
        CacheSizeText.Text = $"当前缓存大小：{FormatBytes(bytes)}";
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            App.Services.Shell.OpenFolder(App.Services.Paths.DataRoot);
        }
        catch (Exception exception)
        {
            AppLog.Error("打开数据目录失败", exception);
        }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            App.Services.Shell.OpenFolder(App.Services.Paths.LogsDirectory);
        }
        catch (Exception exception)
        {
            AppLog.Error("打开日志目录失败", exception);
        }
    }

    private async void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "清理 EPUB 缓存",
            Content = "确定清理所有 EPUB 解压缓存吗？原始书籍文件不会受影响。",
            PrimaryButtonText = "清理",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            try
            {
                await App.Services.EpubCache.ClearAsync();
                await RefreshCacheSizeAsync();
            }
            catch (Exception exception)
            {
                AppLog.Error("清理 EPUB 缓存失败", exception);
                await ShowErrorAsync(exception.Message);
            }
        }
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "操作失败",
            Content = message,
            CloseButtonText = "知道了",
        };
        await dialog.ShowAsync();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.0} KB";
        }

        if (bytes < 1024L * 1024 * 1024)
        {
            return $"{bytes / (1024.0 * 1024):0.0} MB";
        }

        return $"{bytes / (1024.0 * 1024 * 1024):0.00} GB";
    }
}
