using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OctoWatch.Pages;
using Windows.Graphics;
using Windows.UI;

namespace OctoWatch;

public sealed partial class MainWindow : Window
{
    public const int FlyoutWidth = 460;
    public const int FlyoutHeight = 640;

    private WindowBackdrop? _backdrop;
    private TrayIconHost? _tray;
    private bool _allowClose;

    public MainWindow()
    {
        this.InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        // System window icon (taskbar, alt-tab, title bar).
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "OctoWatch.ico");
        if (System.IO.File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
        }

        _backdrop = WindowBackdrop.TryAttach(this, RootGrid);
        AppWindow.Closing += OnWindowClosing;
        AppWindow.Changed += OnWindowChanged;
        _tray = TrayIconHost.Create(ShowFromTray, ExitApplication);
        FeedMonitor.Instance.Start(DispatcherQueue);
        PositionBottomRight(FlyoutWidth, FlyoutHeight);

        NavView.Loaded += (_, _) =>
        {
            NavView.SelectedItem = HomeNav;
        };
    }

    public void ApplyTheme(string theme)
    {
        RootGrid.RequestedTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    public void ReloadForLanguage(string language)
    {
        App.ApplyCulture(language);
        Loc.Reset();
        HomeNav.Content = Loc.Get("Nav_Home/Content");
        AboutNav.Content = Loc.Get("Nav_About/Content");
        ChangelogNav.Content = Loc.Get("Nav_Changelog/Content");
        var current = ContentFrame.CurrentSourcePageType;
        if (current is not null)
        {
            ContentFrame.Navigate(current);
            ContentFrame.BackStack.Clear();
        }
    }

    public void ExitApplication()
    {
        _allowClose = true;
        FeedMonitor.Instance.Stop();
        UpdateToast.Unregister();
        _tray?.Dispose();
        Close();
    }

    public void ShowFromTray()
    {
        PositionBottomRight(FlyoutWidth, FlyoutHeight);
        AppWindow.Show();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        SetForegroundWindow(hwnd);
    }

    public void ApplyBackdropSettings() => _backdrop?.ApplyFromStore();

    public void PreviewBackdrop(bool acrylic, int opacity) => _backdrop?.Preview(acrylic, opacity);

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        Type page = typeof(HomePage);
        if (args.IsSettingsSelected)
            page = typeof(SettingsPage);
        else if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            page = tag switch
            {
                "about" => typeof(AboutPage),
                "changelog" => typeof(ChangelogPage),
                _ => typeof(HomePage),
            };

        if (ContentFrame.CurrentSourcePageType != page)
            ContentFrame.Navigate(page);
    }

    // Collapsed rail stays clear. Open pane becomes a frosted sheet over the feed.
    private void OnPaneOpening(NavigationView sender, object args) => SetPaneGlass(true);

    private void OnPaneClosed(NavigationView sender, object args) => SetPaneGlass(false);

    private void SetPaneGlass(bool open)
    {
        if (NavView.Resources["NavigationViewDefaultPaneBackground"] is not AcrylicBrush brush)
            return;
        if (open)
        {
            brush.TintOpacity = 0.5;
            brush.TintLuminosityOpacity = 0.55;
            brush.FallbackColor =
                Application.Current.Resources["SolidBackgroundFillColorSecondary"] is Color c
                    ? c
                    : Color.FromArgb(255, 32, 32, 32);
        }
        else
        {
            brush.TintOpacity = 0.0;
            brush.TintLuminosityOpacity = 0.0;
            brush.FallbackColor = Colors.Transparent;
        }
    }

    private void PositionBottomRight(int width, int height)
    {
        AppWindow.Resize(new SizeInt32(width, height));
        var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        const int margin = 12;
        int x = work.X + work.Width - width - margin;
        int y = work.Y + work.Height - height - margin;
        AppWindow.Move(new PointInt32(x, y));
    }

    private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
            return;
        args.Cancel = true;
        HideToTray();
    }

    private void OnWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (
            AppWindow.Presenter is OverlappedPresenter p
            && p.State == OverlappedPresenterState.Minimized
        )
        {
            HideToTray();
        }
    }

    private void HideToTray()
    {
        if (
            AppWindow.Presenter is OverlappedPresenter p
            && p.State == OverlappedPresenterState.Minimized
        )
        {
            p.Restore();
        }
        AppWindow.Hide();
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
