using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace OctoWatch.Pages;

public sealed partial class ChangelogPage : Page
{
    public ChangelogPage()
    {
        InitializeComponent();
        Load();
    }

    private void Load()
    {
        var lang = SettingsStore.Load().Language;
        var file = $"changelog/{lang}.md";
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", file);
        if (!File.Exists(path))
            path = Path.Combine(AppContext.BaseDirectory, "Assets", "changelog", "en.md");
        try
        {
            var markdown = File.Exists(path) ? File.ReadAllText(path) : Loc.Get("Changelog_Missing");
            MarkdownRenderer.Render(MarkdownView, markdown);
        }
        catch
        {
            MarkdownView.Blocks.Clear();
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run { Text = Loc.Get("Changelog_Missing") });
            MarkdownView.Blocks.Add(paragraph);
        }
    }
}
