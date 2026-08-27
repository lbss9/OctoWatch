using Microsoft.UI.Xaml.Controls;

namespace OctoWatch.Pages;

public sealed partial class ChangelogPage : Page
{
    public ChangelogPage()
    {
        InitializeComponent();
        ChangelogText.Text = ReadChangelog();
    }

    private static string ReadChangelog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : Loc.Get("Changelog_Missing");
        }
        catch
        {
            return Loc.Get("Changelog_Missing");
        }
    }
}
