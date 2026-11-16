using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace SavorReader.Services;

public static class AppInfo
{
    private static readonly Lazy<string> WebView2VersionValue = new(GetWebView2Version);
    private static readonly Lazy<string> GitShortHashValue = new(GetGitShortHash);

    public static string Version
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "1.0.0" : version.ToString(3);
        }
    }

    public static string DotNetVersion => RuntimeInformation.FrameworkDescription;

    public static string BuildConfiguration
    {
        get
        {
#if DEBUG
            return "Debug";
#else
            return "Release";
#endif
        }
    }

    public static string GitShortHash => GitShortHashValue.Value;

    public static string WindowTitle =>
        $"SavorReader v{Version} · {BuildConfiguration} · {GitShortHash}";

    public static string DisplayVersion =>
        $"{Version} · {BuildConfiguration} · {GitShortHash}";

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

    private static string GetGitShortHash()
    {
        var informationalVersion = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return "nogit";
        }

        var separator = informationalVersion.LastIndexOf('+');
        if (separator < 0 || separator == informationalVersion.Length - 1)
        {
            return "nogit";
        }

        var revision = informationalVersion[(separator + 1)..];
        var hash = new string(revision.TakeWhile(char.IsAsciiHexDigit).ToArray());
        return hash.Length >= 7 ? hash[..7] : "nogit";
    }
}
