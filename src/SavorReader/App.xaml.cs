using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SavorReader.Services;

namespace SavorReader;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("AppDomain 未处理异常", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("未观察的任务异常", args.Exception);
            args.SetObserved();
        };
    }

    public static AppServices Services { get; private set; } = null!;

    public static MainWindow MainWindow { get; private set; } = null!;

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Services = await AppServices.CreateAsync();
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
        catch (Exception exception)
        {
            AppLog.Error("应用启动失败", exception);
            ShowStartupError(exception);
        }
    }

    private static void ShowStartupError(Exception exception)
    {
        var window = new Window
        {
            Title = "SavorReader 启动失败",
            Content = new ScrollViewer
            {
                Padding = new Thickness(32),
                Content = new TextBlock
                {
                    Text = "SavorReader 无法启动。\n\n" + exception.Message,
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };
        window.Activate();
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        AppLog.Error("WinUI 未处理异常", args.Exception);
    }
}
