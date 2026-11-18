using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace SavorReader.Services;

public static class AppInfo
{
    private static readonly Lazy<string> WebView2VersionValue = new(GetWebView2Version);

    public static string Version => BuildInfo.Version;

    public static string DotNetVersion => RuntimeInformation.FrameworkDescription;

    public static string BuildConfiguration => BuildInfo.Configuration;

    public static string GitShortHash => BuildInfo.CommitShort;

    public static string WindowTitle => BuildInfo.WindowTitle;

    public static string DisplayVersion => BuildInfo.DisplayVersion;

    public static string WindowsAppSdkVersion =>
        $"WinUI 3 / Windows App SDK {BuildInfo.WindowsAppSdkVersion}";

    public static string WebView2Version => WebView2VersionValue.Value;

    public static string OperatingSystem
    {
        get
        {
            var version = Environment.OSVersion.Version;
            return $"Windows {version.Major}.{version.Minor} (build {version.Build})";
        }
    }

    public static string Architecture => RuntimeInformation.ProcessArchitecture.ToString();

    public static string LicensePath => Path.Combine(AppContext.BaseDirectory, "LICENSE");

    public static string ThirdPartyNoticesPath =>
        Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.md");

    private static string GetWebView2Version()
    {
        try
        {
            return CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch
        {
            return "未检测到 WebView2 Runtime";
        }
    }
}
