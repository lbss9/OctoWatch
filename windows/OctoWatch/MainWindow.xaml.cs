using System.Runtime.InteropServices;
using H.NotifyIcon;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OctoWatch.Pages;
using Windows.Graphics;
using Windows.UI;
using WinRT;

namespace OctoWatch;

public sealed partial class MainWindow : Window
{
    public const int FlyoutWidth = 460;
    public const int FlyoutHeight = 640;

    public MainWindow()
    {
        this.InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
        }

        TrySetGlassBackdrop();
        AppWindow.Closing += OnWindowClosing;
        AppWindow.Changed += OnWindowChanged;
        SetupTray();
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

    private void PositionBottomRight(int width, int height)
    {
        AppWindow.Resize(new SizeInt32(width, height));
        var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        const int margin = 12;
        int x = work.X + work.Width - width - margin;
        int y = work.Y + work.Height - height - margin;
        AppWindow.Move(new PointInt32(x, y));
    }

    private DesktopAcrylicController? _glass;
    private SystemBackdropConfiguration? _backdropConfig;
    private DispatcherQueueHelper? _dispatcherHelper;

    private void TrySetGlassBackdrop()
    {
        if (!DesktopAcrylicController.IsSupported())
            return;

        _dispatcherHelper = new DispatcherQueueHelper();
        _dispatcherHelper.EnsureDispatcherQueueController();

        _backdropConfig = new SystemBackdropConfiguration { IsInputActive = true };
        Activated += (_, e) =>
            _backdropConfig.IsInputActive =
                e.WindowActivationState != WindowActivationState.Deactivated;
        Closed += (_, _) =>
        {
            _glass?.Dispose();
            _glass = null;
        };
        RootGrid.ActualThemeChanged += (_, _) => UpdateGlassTheme();
        UpdateGlassTheme();

        _glass = new DesktopAcrylicController();
        ApplyGlassColors();
        _glass.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
        _glass.SetSystemBackdropConfiguration(_backdropConfig);
    }

    private void UpdateGlassTheme()
    {
        if (_backdropConfig is null)
            return;
        _backdropConfig.Theme = RootGrid.ActualTheme switch
        {
            ElementTheme.Dark => SystemBackdropTheme.Dark,
            ElementTheme.Light => SystemBackdropTheme.Light,
            _ => SystemBackdropTheme.Default,
        };
        ApplyGlassColors();
    }

    private void ApplyGlassColors()
    {
        if (_glass is null)
            return;
        var dark = RootGrid.ActualTheme != ElementTheme.Light;
        if (dark)
        {
            _glass.TintColor = Color.FromArgb(255, 24, 24, 28);
            _glass.TintOpacity = 0.15f;
            _glass.LuminosityOpacity = 0.25f;
            _glass.FallbackColor = Color.FromArgb(255, 42, 42, 46);
        }
        else
        {
            _glass.TintColor = Color.FromArgb(255, 243, 243, 243);
            _glass.TintOpacity = 0.15f;
            _glass.LuminosityOpacity = 0.35f;
            _glass.FallbackColor = Color.FromArgb(255, 243, 243, 243);
        }
    }

    private bool _allowClose;
    private TaskbarIcon? _tray;

    private void SetupTray()
    {
        var open = new MenuFlyoutItem { Text = Loc.Get("Tray_Open") };
        open.Click += (_, _) => ShowFromTray();
        var exit = new MenuFlyoutItem { Text = Loc.Get("Tray_Exit") };
        exit.Click += (_, _) => ExitApplication();

        var menu = new MenuFlyout();
        menu.Items.Add(open);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(exit);

        _tray = new TaskbarIcon
        {
            ToolTipText = "OctoWatch",
            LeftClickCommand = new RelayCommand(ShowFromTray),
            ContextFlyout = menu,
            IconSource = new GeneratedIconSource
            {
                Text = "O",
                Foreground = new SolidColorBrush(Colors.White),
            },
        };
        _tray.ForceCreate();
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
