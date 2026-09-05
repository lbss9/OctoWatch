using System.Text.RegularExpressions;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace OctoWatch;

/// <summary>
/// Small Markdown renderer for the in-app changelog: headings, bullets, bold,
/// inline code and links. It covers what a changelog needs, nothing more —
/// links open through <see cref="SafeUrl"/> (http/https only), not the shell.
/// </summary>
internal static partial class MarkdownLite
{
    public static void Render(RichTextBlock target, string markdown)
    {
        target.Blocks.Clear();
        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0)
                target.Blocks.Add(new Paragraph { FontSize = 6 }); // vertical breathing room
            else if (line.StartsWith("### "))
                target.Blocks.Add(Heading(line[4..], 15, 10));
            else if (line.StartsWith("## "))
                target.Blocks.Add(Heading(line[3..], 20, 16));
            else if (line.StartsWith("# "))
                target.Blocks.Add(Heading(line[2..], 26, 2));
            else if (line.StartsWith("- ") || line.StartsWith("* "))
                target.Blocks.Add(Bullet(line[2..]));
            else
                target.Blocks.Add(Paragraph(line, 0));
        }
    }

    private static Paragraph Heading(string text, double size, double top)
    {
        var p = new Paragraph { Margin = new Thickness(0, top, 0, 4) };
        p.Inlines.Add(new Run { Text = StripMarks(text), FontSize = size, FontWeight = FontWeights.SemiBold });
        return p;
    }

    private static Paragraph Bullet(string text)
    {
        var p = new Paragraph { Margin = new Thickness(8, 1, 0, 1), TextIndent = -14 };
        p.Inlines.Add(new Run { Text = "•  " });
        AddInlines(p.Inlines, text);
        return p;
    }

    private static Paragraph Paragraph(string text, double top)
    {
        var p = new Paragraph { Margin = new Thickness(0, top, 0, 1) };
        AddInlines(p.Inlines, text);
        return p;
    }

    private static void AddInlines(InlineCollection target, string text)
    {
        var pos = 0;
        foreach (Match m in InlineRegex().Matches(text))
        {
            if (m.Index > pos)
                target.Add(new Run { Text = text[pos..m.Index] });

            if (m.Groups["lt"].Success)
            {
                var link = new Hyperlink();
                link.Inlines.Add(new Run { Text = m.Groups["lt"].Value });
                var url = m.Groups["lu"].Value;
                link.Click += async (_, _) => await SafeUrl.OpenAsync(url);
                target.Add(link);
            }
            else if (m.Groups["b"].Success)
            {
                target.Add(new Run { Text = m.Groups["b"].Value, FontWeight = FontWeights.SemiBold });
            }
            else if (m.Groups["c"].Success)
            {
                target.Add(new Run { Text = m.Groups["c"].Value, FontFamily = new FontFamily("Consolas") });
            }

            pos = m.Index + m.Length;
        }
        if (pos < text.Length)
            target.Add(new Run { Text = text[pos..] });
    }

    private static string StripMarks(string s) => s.Replace("**", "").Replace("`", "");

    // [text](url) | **bold** | `code`
    [GeneratedRegex(@"\[(?<lt>[^\]]+)\]\((?<lu>[^)]+)\)|\*\*(?<b>[^*]+)\*\*|`(?<c>[^`]+)`")]
    private static partial Regex InlineRegex();
}
