using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace SavorReader.Services;

public static class AppInfo
{
    private static readonly Lazy<string> WebView2VersionValue = new(GetWebView2Version);

    public static string Version
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "1.0.0" : version.ToString(3);
        }
    }

    public static string DotNetVersion => RuntimeInformation.FrameworkDescription;

    public static string WindowsAppSdkVersion => "WinUI 3 / Windows App SDK 2.6.5";

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
