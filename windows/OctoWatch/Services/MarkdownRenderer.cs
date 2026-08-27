using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace OctoWatch;

internal static class MarkdownRenderer
{
    public static void Render(RichTextBlock target, string markdown)
    {
        target.Blocks.Clear();
        foreach (var block in MarkdownLite.Parse(markdown))
        {
            switch (block)
            {
                case MdHeading heading:
                    target.Blocks.Add(Heading(heading));
                    break;
                case MdParagraph paragraph:
                    target.Blocks.Add(Paragraph(paragraph.Spans, 0));
                    break;
                case MdList list:
                    foreach (var item in list.Items)
                        target.Blocks.Add(Paragraph(item, 0, "• "));
                    break;
            }
        }
    }

    private static Paragraph Heading(MdHeading heading)
    {
        var paragraph = Paragraph(heading.Spans, heading.Level switch
        {
            1 => 8,
            2 => 14,
            _ => 10,
        });
        paragraph.FontWeight = FontWeights.SemiBold;
        paragraph.FontSize = heading.Level switch
        {
            1 => 22,
            2 => 18,
            _ => 15,
        };
        return paragraph;
    }

    private static Paragraph Paragraph(IReadOnlyList<MdSpan> spans, int top, string? prefix = null)
    {
        var paragraph = new Paragraph { Margin = new Microsoft.UI.Xaml.Thickness(0, top, 0, 4) };
        if (!string.IsNullOrEmpty(prefix))
            paragraph.Inlines.Add(new Run { Text = prefix });
        foreach (var span in spans)
            AddSpan(paragraph, span);
        return paragraph;
    }

    private static void AddSpan(Paragraph paragraph, MdSpan span)
    {
        switch (span)
        {
            case MdBold bold:
                paragraph.Inlines.Add(new Run { Text = bold.Text, FontWeight = FontWeights.SemiBold });
                break;
            case MdLink link:
                var hyper = new Hyperlink();
                hyper.Inlines.Add(new Run { Text = link.Text });
                var url = link.Url;
                hyper.Click += (_, _) => _ = SafeUrl.OpenAsync(url);
                paragraph.Inlines.Add(hyper);
                break;
            case MdText text:
                paragraph.Inlines.Add(new Run { Text = text.Text });
                break;
        }
    }
}
