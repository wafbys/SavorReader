using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using SavorReader.Core.Models;
using SavorReader.Services;
using SavorReader.ViewModels;

namespace SavorReader.Views;

public sealed partial class EpubReaderPage : Page
{
    private readonly DispatcherTimer _saveTimer;
    private long _bookId;
    private Book? _book;
    private ReaderSettings _settings = ReaderSettings.Default;
    private bool _loadingSettings;
    private bool _dirty;
    private int _lastSpineIndex;
    private double _lastRatio;
    private double _lastPercent;
    private bool _webViewReady;

    public EpubReaderPage()
    {
        InitializeComponent();
        _saveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2),
        };
        _saveTimer.Tick += SaveTimer_Tick;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is long bookId)
        {
            _bookId = bookId;
            await InitializeAsync();
        }
        else
        {
            ShowError("没有收到书籍编号。");
        }
    }

    protected override async void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _saveTimer.Stop();
        await SaveProgressAsync();

        if (_webViewReady && ReaderWebView.CoreWebView2 is { } core)
        {
            core.WebMessageReceived -= Core_WebMessageReceived;
            core.ProcessFailed -= Core_ProcessFailed;
            core.NavigationCompleted -= Core_NavigationCompleted;
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
                ShowError("原始 EPUB 文件已经不存在。\n\n可以把文件放回原位置，或回到书库重新导入。");
                return;
            }

            _settings = await App.Services.Settings.GetReaderSettingsAsync();
            ApplySettingsToControls(_settings);
            TitleText.Text = _book.Title;
            AuthorText.Text = string.IsNullOrWhiteSpace(_book.Author) ? "未知作者" : _book.Author;
            ProgressText.Text = $"{_book.Progress:P0}";

            LoadingOverlay.Visibility = Visibility.Visible;
            ErrorOverlay.Visibility = Visibility.Collapsed;

            var result = await Task.Run(() => App.Services.EpubReader.Read(_book.FilePath));
            TocList.ItemsSource = result.Content.TableOfContents
                .Select(item => new TocItemViewModel(
                    item.Title,
                    new Thickness(Math.Min(item.Depth, 5) * 14, 3, 0, 3),
                    item.SpineIndex,
                    item.Anchor))
                .ToArray();

            await App.Services.Progress.MarkOpenedAsync(_book);
            var setup = await App.Services.EpubReaderAssets.PrepareAsync(_book, result.Content, _settings);
            var environment = await App.Services.WebView2.GetAsync();
            await ReaderWebView.EnsureCoreWebView2Async(environment);
            var core = ReaderWebView.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.WebMessageReceived += Core_WebMessageReceived;
            core.ProcessFailed += Core_ProcessFailed;
            core.NavigationCompleted += Core_NavigationCompleted;
            core.AddWebResourceRequestedFilter(
                "*",
                CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, args) =>
            {
                if (Uri.TryCreate(args.Request.Uri, UriKind.Absolute, out var uri)
                    && uri.Scheme is "http" or "https"
                    && !uri.Host.Equals("book.local", StringComparison.OrdinalIgnoreCase))
                {
                    args.Response = environment.CreateWebResourceResponse(
                        null,
                        403,
                        "Blocked",
                        "Content-Type: text/plain");
                }
            };
            core.SetVirtualHostNameToFolderMapping(
                "book.local",
                setup.CacheRoot,
                CoreWebView2HostResourceAccessKind.DenyCors);
            core.Navigate(setup.ShellUrl);

            _webViewReady = true;
            _saveTimer.Start();
        }
        catch (Exception exception)
        {
            AppLog.Error($"打开 EPUB 失败：{_book?.FilePath}", exception);
            ShowError($"无法打开 EPUB：\n{exception.Message}");
        }
    }

    private void Core_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeElement))
            {
                return;
            }

            switch (typeElement.GetString())
            {
                case "ready":
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    AppLog.Info("EPUB reader ready.");
                    break;

                case "progress":
                    _lastSpineIndex = root.TryGetProperty("spineIndex", out var spine)
                        ? Math.Max(0, spine.GetInt32())
                        : 0;
                    _lastRatio = root.TryGetProperty("chapterRatio", out var ratio)
                        ? Math.Clamp(ratio.GetDouble(), 0, 1)
                        : 0;
                    _lastPercent = root.TryGetProperty("percent", out var percent)
                        ? Math.Clamp(percent.GetDouble(), 0, 1)
                        : 0;
                    _dirty = true;
                    ProgressText.Text = $"{_lastPercent:P0}";
                    break;

                case "selection":
                    // Reserved for the future highlight/note UI. The text and
                    // spine index already arrive from reader.js.
                    break;

                case "externalLink":
                    if (root.TryGetProperty("url", out var url))
                    {
                        AppLog.Info($"EPUB 外部链接被拦截：{url.GetString()}");
                    }

                    break;

                case "error":
                    ShowError(root.TryGetProperty("message", out var message)
                        ? message.GetString() ?? "阅读器内部错误。"
                        : "阅读器内部错误。");
                    break;
            }
        }
        catch (Exception exception)
        {
            AppLog.Warn("无法解析阅读器消息", exception);
        }
    }

    private void Core_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            ShowError($"阅读器页面加载失败：{e.WebErrorStatus}");
        }
    }

    private void Core_ProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        AppLog.Error($"WebView2 进程失败：{e.ProcessFailedKind}");
        ShowError("阅读器渲染进程异常退出，请返回书库后重试。");
    }

    private async void SaveTimer_Tick(object? sender, object e) => await SaveProgressAsync();

    private async Task SaveProgressAsync()
    {
        if (_book is null || !_dirty)
        {
            return;
        }

        _dirty = false;
        try
        {
            await App.Services.Progress.SaveEpubProgressAsync(
                _book,
                _lastSpineIndex,
                _lastRatio,
                _lastPercent);
        }
        catch (Exception exception)
        {
            AppLog.Warn("保存 EPUB 进度失败", exception);
        }
    }

    private async void Back_Click(object sender, RoutedEventArgs e)
    {
        _saveTimer.Stop();
        await SaveProgressAsync();
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            Frame.Navigate(typeof(LibraryPage));
        }
    }

    private async void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings)
        {
            return;
        }

        NightToggle.IsOn = !NightToggle.IsOn;
        await ApplySettingsAsync();
    }

    private async void SettingsControl_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingSettings)
        {
            await ApplySettingsAsync();
        }
    }

    private async void SettingsSlider_Changed(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_loadingSettings)
        {
            await ApplySettingsAsync();
        }
    }

    private async void NightToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loadingSettings)
        {
            await ApplySettingsAsync();
        }
    }

    private async void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        ApplySettingsToControls(ReaderSettings.Default);
        await ApplySettingsAsync();
    }

    private async Task ApplySettingsAsync()
    {
        if (_loadingSettings || _book is null)
        {
            return;
        }

        var settings = new ReaderSettings
        {
            Theme = NightToggle.IsOn ? ReaderTheme.Night : ReaderTheme.Day,
            Font = FontCombo.SelectedIndex == 1 ? ReaderFont.Sans : ReaderFont.Serif,
            FontSize = FontSizeSlider.Value,
            LineHeight = Math.Round(LineHeightSlider.Value, 1),
            Margin = MarginSlider.Value,
        }.Clamp();

        _settings = settings;
        ThemeButton.Content = settings.Theme == ReaderTheme.Night ? "日间" : "夜间";

        try
        {
            await App.Services.Settings.SaveReaderSettingsAsync(settings);
            PostSettings(settings);
        }
        catch (Exception exception)
        {
            AppLog.Warn("保存阅读设置失败", exception);
        }
    }

    private void ApplySettingsToControls(ReaderSettings settings)
    {
        _loadingSettings = true;
        try
        {
            var clamped = settings.Clamp();
            FontCombo.SelectedIndex = clamped.Font == ReaderFont.Sans ? 1 : 0;
            FontSizeSlider.Value = clamped.FontSize;
            LineHeightSlider.Value = clamped.LineHeight;
            MarginSlider.Value = clamped.Margin;
            NightToggle.IsOn = clamped.Theme == ReaderTheme.Night;
            ThemeButton.Content = clamped.Theme == ReaderTheme.Night ? "日间" : "夜间";
        }
        finally
        {
            _loadingSettings = false;
        }
    }

    private void PostSettings(ReaderSettings settings)
    {
        if (ReaderWebView.CoreWebView2 is not { } core)
        {
            return;
        }

        var json = JsonSerializer.Serialize(new
        {
            type = "settings",
            settings = new
            {
                theme = settings.ThemeName,
                font = settings.FontName,
                fontSize = settings.FontSize,
                lineHeight = settings.LineHeight,
                margin = settings.Margin,
            },
        });
        core.PostWebMessageAsJson(json);
    }

    private void TocList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TocList.SelectedItem is not TocItemViewModel item)
        {
            return;
        }

        TocList.SelectedItem = null;
        TocButton.Flyout?.Hide();
        GoTo(item.SpineIndex, item.Anchor);
    }

    private void GoTo(int spineIndex, string? anchor)
    {
        if (ReaderWebView.CoreWebView2 is not { } core)
        {
            return;
        }

        var json = JsonSerializer.Serialize(new
        {
            type = "goto",
            spineIndex,
            anchor,
        });
        core.PostWebMessageAsJson(json);
    }

    private void ShowError(string message)
    {
        LoadingOverlay.Visibility = Visibility.Collapsed;
        ErrorText.Text = message;
        ErrorOverlay.Visibility = Visibility.Visible;
    }
}
