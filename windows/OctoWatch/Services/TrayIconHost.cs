using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace OctoWatch;

/// <summary>Owns the NotifyIcon lifetime so MainWindow stays focused on the shell.</summary>
internal sealed class TrayIconHost : IDisposable
{
    private readonly TaskbarIcon _tray;

    private TrayIconHost(TaskbarIcon tray) => _tray = tray;

    public static TrayIconHost Create(Action show, Action exit)
    {
        var open = new MenuFlyoutItem { Text = Loc.Get("Tray_Open") };
        open.Click += (_, _) => show();
        var quit = new MenuFlyoutItem { Text = Loc.Get("Tray_Exit") };
        quit.Click += (_, _) => exit();

        var menu = new MenuFlyout();
        menu.Items.Add(open);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(quit);

        var tray = new TaskbarIcon
        {
            ToolTipText = "OctoWatch",
            LeftClickCommand = new RelayCommand(show),
            ContextFlyout = menu,
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/OctoWatch.ico")),
        };
        tray.ForceCreate();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "OctoWatch.ico");
        try
        {
            if (File.Exists(iconPath))
                tray.Icon = new System.Drawing.Icon(iconPath, 32, 32);
        }
        catch
        {
            // Unpackaged ms-appx can yield a blank tray glyph; keep the BitmapImage.
        }

        return new TrayIconHost(tray);
    }

    public void Dispose() => _tray.Dispose();
}
