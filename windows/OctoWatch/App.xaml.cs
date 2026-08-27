using System.Globalization;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.Resources.Core;

namespace OctoWatch;

public partial class App : Application
{
    public static MainWindow? Main { get; private set; }

    public App()
    {
        Velopack.VelopackApp.Build().Run(); // must be first — handles install/update hooks
        this.UnhandledException += (_, e) =>
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OctoWatch",
                "crash.log"
            );
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, $"{e.Message}\n{e.Exception}");
        };
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var settings = SettingsStore.Load();
        ApplyCulture(settings.Language);
        UpdateToast.Register();
        Main = new MainWindow();
        Main.ApplyTheme(settings.Theme);
        Main.Activate();
        if (settings.AutoUpdate)
            _ = UpdateService.CheckAndApplyAsync();
    }

    public static void ApplyCulture(string language)
    {
        try
        {
            var culture = new CultureInfo(language);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            ResourceContext.SetGlobalQualifierValue("Language", language);
        }
        catch
        {
        }
    }
}
