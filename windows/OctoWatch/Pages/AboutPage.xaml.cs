using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OctoWatch.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        var version = typeof(App).Assembly.GetName().Version;
        var display = version is null ? "0.1.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        VersionText.Text = string.Format(Loc.Get("About_Version"), display);
    }

    private async void OnGitHubLink(object sender, RoutedEventArgs e) =>
        await SafeUrl.OpenAsync(
            "https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps#device-flow"
        );
}

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        var version = typeof(App).Assembly.GetName().Version;
        var display = version is null ? "0.1.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        VersionText.Text = string.Format(Loc.Get("About_Version"), display);
    }
}
