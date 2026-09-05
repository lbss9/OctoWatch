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

    private static readonly string IconPath = Path.Combine(
        AppContext.BaseDirectory,
        "Assets",
        "OctoWatch.ico"
    );

    public MainWindow()
    {
        this.InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        // System window icon (taskbar, alt-tab).
        if (File.Exists(IconPath))
            AppWindow.SetIcon(IconPath);

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

    // Collapsed rail = transparent (glass, homogeneous). Open pane = dark frosted
    // glass that blurs the content behind it (readable, no bleed-through).
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
        ApplyBackdropSettings();
        _glass.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
        _glass.SetSystemBackdropConfiguration(_backdropConfig);
    }

    /// <summary>
    /// Reloads the transparency preferences and reapplies them. Called by the
    /// Settings page when the user clicks "Apply".
    /// </summary>
    public void ApplyBackdropSettings()
    {
        var settings = SettingsStore.Load();
        _acrylicOn = settings.AcrylicEnabled;
        _opacityPct = Math.Clamp(settings.BackgroundOpacity, 0, 100);
        ApplyGlassColors();
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

    private bool _acrylicOn = true;
    private int _opacityPct = 30;

    private void ApplyGlassColors()
    {
        var dark = RootGrid.ActualTheme != ElementTheme.Light;
        var baseColor = dark
            ? Color.FromArgb(255, 24, 24, 28)
            : Color.FromArgb(255, 243, 243, 243);

        // The acrylic controller keeps a fixed clean glass; the user-facing opacity
        // is applied as an alpha LAYER on top, because a XAML brush is reliable
        // whereas the controller's own TintOpacity is ignored in this setup.
        if (_glass is not null)
        {
            // Clean glass (blur with almost no frost), like Windows Terminal's acrylic.
            // The real opacity comes from the alpha layer below, to match the Terminal.
            _glass.TintColor = baseColor;
            _glass.FallbackColor = dark
                ? Color.FromArgb(255, 42, 42, 46)
                : Color.FromArgb(255, 243, 243, 243);
            _glass.TintOpacity = 0.0f;
            _glass.LuminosityOpacity = 0.10f;
        }

        // Opacity layer over the glass (0-100%). Acrylic OFF (or 100%) = opaque;
        // low opacity = very translucent, showing the blurred desktop (like the Terminal).
        var alpha = _acrylicOn && _glass is not null
            ? (byte)Math.Clamp(_opacityPct * 255 / 100, 0, 255)
            : (byte)255;
        RootGrid.Background = new SolidColorBrush(
            Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B)
        );
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
            IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(
                new Uri("ms-appx:///Assets/OctoWatch.ico")
            ),
        };
        _tray.ForceCreate();

        // Fallback if the packaged-uri icon doesn't render on unpackaged builds.
        try
        {
            if (File.Exists(IconPath))
                _tray.Icon = new System.Drawing.Icon(IconPath, 32, 32);
        }
        catch
        {
            // keep the IconSource icon
        }
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
