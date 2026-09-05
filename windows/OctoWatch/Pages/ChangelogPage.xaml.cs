using Microsoft.UI.Xaml.Controls;

namespace OctoWatch.Pages;

public sealed partial class ChangelogPage : Page
{
    public ChangelogPage()
    {
        InitializeComponent();
        MarkdownLite.Render(ChangelogView, ReadChangelog());
    }

    /// <summary>Loads the changelog for the current language, falling back to English.</summary>
    private static string ReadChangelog()
    {
        var lang = SettingsStore.Load().Language;
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "changelog", $"{lang}.md"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "changelog", "en.md"),
        };
        foreach (var path in candidates)
        {
            try
            {
                if (File.Exists(path))
                    return File.ReadAllText(path);
            }
            catch
            {
                // try the next candidate
            }
        }
        return Loc.Get("Changelog_Missing");
    }
}
