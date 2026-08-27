using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinRT;

namespace OctoWatch;

/// <summary>Desktop acrylic plus a XAML alpha overlay (Windows Terminal-style opacity).</summary>
internal sealed class WindowBackdrop
{
    private readonly Window _window;
    private readonly FrameworkElement _root;
    private DesktopAcrylicController? _glass;
    private SystemBackdropConfiguration? _backdropConfig;
    private DispatcherQueueHelper? _dispatcherHelper;
    private bool _acrylicOn = true;
    private int _opacityPct = 30;

    private WindowBackdrop(Window window, FrameworkElement root)
    {
        _window = window;
        _root = root;
    }

    public static WindowBackdrop? TryAttach(Window window, FrameworkElement root)
    {
        if (!DesktopAcrylicController.IsSupported())
            return null;

        var backdrop = new WindowBackdrop(window, root);
        backdrop.Attach();
        return backdrop;
    }

    public void ApplyFromStore()
    {
        var settings = SettingsStore.Load();
        Preview(settings.AcrylicEnabled, settings.BackgroundOpacity);
    }

    public void Preview(bool acrylic, int opacity)
    {
        _acrylicOn = acrylic;
        _opacityPct = Math.Clamp(opacity, 0, 100);
        ApplyGlassColors();
    }

    public void Dispose()
    {
        _glass?.Dispose();
        _glass = null;
    }

    private void Attach()
    {
        _dispatcherHelper = new DispatcherQueueHelper();
        _dispatcherHelper.EnsureDispatcherQueueController();

        _backdropConfig = new SystemBackdropConfiguration { IsInputActive = true };
        _window.Activated += (_, e) =>
            _backdropConfig.IsInputActive =
                e.WindowActivationState != WindowActivationState.Deactivated;
        _window.Closed += (_, _) => Dispose();
        _root.ActualThemeChanged += (_, _) => UpdateGlassTheme();
        UpdateGlassTheme();

        _glass = new DesktopAcrylicController();
        ApplyFromStore();
        _glass.AddSystemBackdropTarget(_window.As<ICompositionSupportsSystemBackdrop>());
        _glass.SetSystemBackdropConfiguration(_backdropConfig);
    }

    private void UpdateGlassTheme()
    {
        if (_backdropConfig is null)
            return;
        _backdropConfig.Theme = _root.ActualTheme switch
        {
            ElementTheme.Dark => SystemBackdropTheme.Dark,
            ElementTheme.Light => SystemBackdropTheme.Light,
            _ => SystemBackdropTheme.Default,
        };
        ApplyGlassColors();
    }

    private void ApplyGlassColors()
    {
        var dark = _root.ActualTheme != ElementTheme.Light;
        var baseColor = dark
            ? Color.FromArgb(255, 24, 24, 28)
            : Color.FromArgb(255, 243, 243, 243);

        // Keep the acrylic controller as a light blur. User opacity is a XAML
        // alpha layer on top — TintOpacity on the controller is not reliable.
        if (_glass is not null)
        {
            _glass.TintColor = baseColor;
            _glass.FallbackColor = dark
                ? Color.FromArgb(255, 42, 42, 46)
                : Color.FromArgb(255, 243, 243, 243);
            _glass.TintOpacity = 0.0f;
            _glass.LuminosityOpacity = 0.10f;
        }

        var alpha = _acrylicOn && _glass is not null
            ? (byte)Math.Clamp(_opacityPct * 255 / 100, 0, 255)
            : (byte)255;
        _root.Background = new SolidColorBrush(
            Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B)
        );
    }
}
