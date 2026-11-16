using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SavorReader.Core.Models;
using SavorReader.Services;

namespace SavorReader.Views;

public sealed partial class SettingsPage : Page
{
    private CleanupReport? _slimmingReport;
    private bool _slimmingBusy;

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        DataPathText.Text = App.Services.Paths.DataRoot;
        AboutVersionText.Text = $"SavorReader {AppInfo.DisplayVersion} · 本地 EPUB/PDF 书库与阅读器";
        await RefreshSlimmingAsync();
    }

    private void OpenAbout_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(AboutPage));

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

    private async void RefreshSlimming_Click(object sender, RoutedEventArgs e) =>
        await RefreshSlimmingAsync();

    private async Task RefreshSlimmingAsync()
    {
        if (_slimmingBusy)
        {
            return;
        }

        SetSlimmingBusy(true);
        try
        {
            _slimmingReport = await App.Services.Slimming.ScanAsync();
            var webViewScheduled = File.Exists(App.Services.Paths.DeferredWebViewCleanupMarker);
            SlimmingSummaryText.Text = FormatSlimmingReport(_slimmingReport, webViewScheduled);
            SlimmingCleanButton.IsEnabled = _slimmingReport.ImmediateBytes > 0
                || (_slimmingReport.DeferredBytes > 0 && !webViewScheduled);
        }
        catch (Exception exception)
        {
            AppLog.Error("扫描瘦身空间失败", exception);
            SlimmingSummaryText.Text = $"扫描失败：{exception.Message}";
            _slimmingReport = null;
            SlimmingCleanButton.IsEnabled = false;
        }
        finally
        {
            SetSlimmingBusy(false);
        }
    }

    private async void CleanSlimming_Click(object sender, RoutedEventArgs e)
    {
        if (_slimmingBusy || _slimmingReport is null)
        {
            return;
        }

        var report = _slimmingReport;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "瘦身",
            Content = $"""
                将清理 EPUB 解压缓存、未引用的书籍副本/封面和日志。
                预计立即释放约 {FormatBytes(report.ImmediateBytes)}。
                WebView2 浏览器缓存约 {FormatBytes(report.DeferredBytes)}，会在下次启动时清理。

                书库中正在使用的书籍和封面不会被删除。
                """,
            PrimaryButtonText = "清理",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        SetSlimmingBusy(true);
        CleanupResult result;
        try
        {
            result = await App.Services.Slimming.CleanAsync(includeWebView2: true);
        }
        catch (Exception exception)
        {
            AppLog.Error("瘦身失败", exception);
            SetSlimmingBusy(false);
            await RefreshSlimmingAsync();
            await ShowErrorAsync(exception.Message);
            return;
        }

        SetSlimmingBusy(false);
        await RefreshSlimmingAsync();

        var message = $"已清理 {FormatBytes(result.RemovedBytes)}，共 {result.RemovedFiles} 个文件。";
        if (result.ScheduledBytes > 0)
        {
            message += $"\nWebView2 浏览器缓存约 {FormatBytes(result.ScheduledBytes)}，将在下次启动时清理。";
        }

        if (result.Errors.Count > 0)
        {
            message += "\n\n以下项目未能清理：\n" + string.Join('\n', result.Errors.Take(8));
        }

        var resultDialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = result.Errors.Count > 0 ? "瘦身完成（部分项失败）" : "瘦身完成",
            Content = message,
            CloseButtonText = "知道了",
        };
        await resultDialog.ShowAsync();
    }

    private void SetSlimmingBusy(bool busy)
    {
        _slimmingBusy = busy;
        SlimmingProgress.IsActive = busy;
        SlimmingScanButton.IsEnabled = !busy;
        if (busy)
        {
            SlimmingCleanButton.IsEnabled = false;
        }
    }

    private static string FormatSlimmingReport(CleanupReport report, bool webViewScheduled)
    {
        if (!report.HasWork && !webViewScheduled)
        {
            return "没有发现可清理的缓存或孤立文件。";
        }

        var lines = new List<string>();
        if (report.ImmediateBytes > 0)
        {
            lines.Add($"可立即释放：{FormatBytes(report.ImmediateBytes)}");
        }

        if (report.DeferredBytes > 0)
        {
            lines.Add(webViewScheduled
                ? $"WebView2 浏览器缓存：已安排下次启动清理（{FormatBytes(report.DeferredBytes)}）"
                : $"下次启动可释放：{FormatBytes(report.DeferredBytes)}（WebView2 浏览器缓存）");
        }

        foreach (var target in report.Targets.Where(target => target.Bytes > 0))
        {
            var suffix = target.Kind == CleanupTargetKind.AfterRestart
                ? webViewScheduled ? "（已安排）" : "（下次启动）"
                : string.Empty;
            lines.Add($"{target.Name}：{FormatBytes(target.Bytes)}，{target.FileCount} 个文件{suffix}");
        }

        return lines.Count == 0
            ? "没有发现可清理的缓存或孤立文件。"
            : string.Join(Environment.NewLine, lines);
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
