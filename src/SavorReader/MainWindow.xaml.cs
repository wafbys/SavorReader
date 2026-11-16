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

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");

        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min(1520, (int)(workArea.Width * 0.9));
        var height = Math.Min(960, (int)(workArea.Height * 0.9));
        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + (workArea.Width - width) / 2,
            workArea.Y + (workArea.Height - height) / 2,
            width,
            height));
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        AppLog.Info("MainWindow NavView loaded.");
        if (ContentFrame.Content is null)
        {
            NavView.SelectedItem = LibraryNavItem;
            NavigateTo(typeof(LibraryPage));
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
        NavView.IsPaneVisible = !reading;
        NavView.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;

        if (reading)
        {
            return;
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
