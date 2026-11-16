using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SavorReader.Services;

namespace SavorReader.Views;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        VersionText.Text = $"版本 {AppInfo.Version}";
        DotNetText.Text = AppInfo.DotNetVersion;
        WinUiText.Text = AppInfo.WindowsAppSdkVersion;
        WebView2Text.Text = AppInfo.WebView2Version;
        OperatingSystemText.Text = AppInfo.OperatingSystem;
        ArchitectureText.Text = AppInfo.Architecture;
        DataPathText.Text = App.Services.Paths.DataRoot;
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
            _ = ShowMessageAsync("操作失败", exception.Message);
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
            _ = ShowMessageAsync("操作失败", exception.Message);
        }
    }

    private void OpenLicense_Click(object sender, RoutedEventArgs e) =>
        OpenLocalDocument(AppInfo.LicensePath, "MIT License");

    private void OpenThirdPartyNotices_Click(object sender, RoutedEventArgs e) =>
        OpenLocalDocument(AppInfo.ThirdPartyNoticesPath, "第三方声明");

    private void OpenLocalDocument(string path, string title)
    {
        if (!File.Exists(path))
        {
            _ = ShowMessageAsync("文件不存在", $"{title} 文件没有随应用部署：\n{path}");
            return;
        }

        try
        {
            App.Services.Shell.OpenWithDefaultApp(path);
        }
        catch (Exception exception)
        {
            AppLog.Error($"打开 {title} 失败", exception);
            _ = ShowMessageAsync("无法打开", exception.Message);
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
}
