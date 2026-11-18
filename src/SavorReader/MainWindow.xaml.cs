using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using SavorReader.Services;
using SavorReader.Views;
using Windows.Graphics;

namespace SavorReader;

public sealed partial class MainWindow : Window
{
    private bool _navigating;
    private bool _paneStateReady;
    private bool _suppressPaneStateSave;
    private bool _readerActive;
    private bool _paneOpenBeforeReader;

    public MainWindow()
    {
        InitializeComponent();
        NavView.RegisterPropertyChangedCallback(
            NavigationView.IsPaneOpenProperty,
            OnPaneOpenChanged);
        var windowTitle = AppInfo.WindowTitle;
        Title = windowTitle;
        AppTitleBar.Title = windowTitle;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min(1520, (int)(workArea.Width * 0.9));
        var height = Math.Min(960, (int)(workArea.Height * 0.9));
        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + (workArea.Width - width) / 2,
            workArea.Y + (workArea.Height - height) / 2,
            width,
            height));
    }

    private async void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        AppLog.Info("MainWindow NavView loaded.");
        if (!_paneStateReady)
        {
            await RestorePaneStateAsync();
        }

        if (ContentFrame.Content is null)
        {
            NavView.SelectedItem = LibraryNavItem;
            NavigateTo(typeof(LibraryPage));
        }
    }

    private async Task RestorePaneStateAsync()
    {
        try
        {
            var isOpen = await App.Services.Settings.GetNavigationPaneOpenAsync();
            _suppressPaneStateSave = true;
            NavView.IsPaneOpen = isOpen;
        }
        catch (Exception exception)
        {
            AppLog.Warn("恢复左侧导航栏状态失败", exception);
        }
        finally
        {
            _suppressPaneStateSave = false;
            _paneStateReady = true;
        }
    }

    private void OnPaneOpenChanged(DependencyObject sender, DependencyProperty dependencyProperty)
    {
        if (!_paneStateReady || _suppressPaneStateSave)
        {
            return;
        }

        _ = SavePaneStateAsync(NavView.IsPaneOpen);
    }

    private async Task SavePaneStateAsync(bool isOpen)
    {
        try
        {
            await App.Services.Settings.SetNavigationPaneOpenAsync(isOpen);
        }
        catch (Exception exception)
        {
            AppLog.Warn("保存左侧导航栏状态失败", exception);
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_navigating || args.SelectedItemContainer?.Tag is not string tag)
        {
            return;
        }

        var pageType = tag switch
        {
            "library" => typeof(LibraryPage),
            "settings" => typeof(SettingsPage),
            "about" => typeof(AboutPage),
            _ => null,
        };

        if (pageType is not null)
        {
            NavigateTo(pageType);
        }
    }

    private void ContentFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        AppLog.Info($"MainWindow navigated to {e.SourcePageType.Name}.");
        var reading = e.SourcePageType == typeof(EpubReaderPage) || e.SourcePageType == typeof(PdfReaderPage);
        NavView.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;

        if (reading)
        {
            if (!_readerActive)
            {
                _paneOpenBeforeReader = NavView.IsPaneOpen;
                _readerActive = true;
            }

            _suppressPaneStateSave = true;
            NavView.IsPaneVisible = false;
            return;
        }

        NavView.IsPaneVisible = true;
        if (_readerActive)
        {
            _readerActive = false;
            if (_paneStateReady)
            {
                _suppressPaneStateSave = true;
                NavView.IsPaneOpen = _paneOpenBeforeReader;
            }

            _suppressPaneStateSave = false;
        }

        _navigating = true;
        try
        {
            if (e.SourcePageType == typeof(LibraryPage))
            {
                NavView.SelectedItem = LibraryNavItem;
            }
            else if (e.SourcePageType == typeof(SettingsPage))
            {
                NavView.SelectedItem = SettingsNavItem;
            }
            else if (e.SourcePageType == typeof(AboutPage))
            {
                NavView.SelectedItem = AboutNavItem;
            }
        }
        finally
        {
            _navigating = false;
        }
    }

    private void NavigateTo(Type pageType, object? parameter = null)
    {
        if (ContentFrame.CurrentSourcePageType == pageType)
        {
            return;
        }

        ContentFrame.Navigate(pageType, parameter, new SuppressNavigationTransitionInfo());
    }
}
