namespace OctoWatch;

public abstract record MdBlock;

public sealed record MdHeading(int Level, IReadOnlyList<MdSpan> Spans) : MdBlock;

public sealed record MdParagraph(IReadOnlyList<MdSpan> Spans) : MdBlock;

public sealed record MdList(IReadOnlyList<IReadOnlyList<MdSpan>> Items) : MdBlock;

public abstract record MdSpan;

public sealed record MdText(string Text) : MdSpan;

public sealed record MdBold(string Text) : MdSpan;

public sealed record MdLink(string Text, string Url) : MdSpan;

/// <summary>Small markdown subset: headings, bullets, bold, and links.</summary>
public static class MarkdownLite
{
    public static IReadOnlyList<MdBlock> Parse(string markdown)
    {
        var blocks = new List<MdBlock>();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                blocks.Add(new MdHeading(3, ParseSpans(line[4..])));
                i++;
                continue;
            }
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                blocks.Add(new MdHeading(2, ParseSpans(line[3..])));
                i++;
                continue;
            }
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                blocks.Add(new MdHeading(1, ParseSpans(line[2..])));
                i++;
                continue;
            }
            if (IsBullet(line))
            {
                var items = new List<IReadOnlyList<MdSpan>>();
                while (i < lines.Length && IsBullet(lines[i]))
                {
                    items.Add(ParseSpans(BulletText(lines[i])));
                    i++;
                }
                blocks.Add(new MdList(items));
                continue;
            }

            var text = line.Trim();
            i++;
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]) && !IsBullet(lines[i]) && !IsHeading(lines[i]))
            {
                text += " " + lines[i].Trim();
                i++;
            }
            blocks.Add(new MdParagraph(ParseSpans(text)));
        }
        return blocks;
    }

    public static IReadOnlyList<MdSpan> ParseSpans(string text)
    {
        var spans = new List<MdSpan>();
        var i = 0;
        while (i < text.Length)
        {
            if (TryLink(text, i, out var link, out var consumed) || TryBold(text, i, out link, out consumed))
            {
                spans.Add(link);
                i += consumed;
                continue;
            }
            var next = NextMarkup(text, i + 1);
            spans.Add(new MdText(text[i..next]));
            i = next;
        }
        return spans;
    }

    private static bool IsBullet(string line)
    {
        var t = line.TrimStart();
        return t.StartsWith("- ", StringComparison.Ordinal) || t.StartsWith("* ", StringComparison.Ordinal);
    }

    private static bool IsHeading(string line) =>
        line.StartsWith("# ", StringComparison.Ordinal)
        || line.StartsWith("## ", StringComparison.Ordinal)
        || line.StartsWith("### ", StringComparison.Ordinal);

    private static string BulletText(string line)
    {
        var t = line.TrimStart();
        return t.Length >= 2 ? t[2..] : t;
    }

    private static int NextMarkup(string text, int start)
    {
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '*' || text[i] == '[')
                return i;
        }
        return text.Length;
    }

    private static bool TryBold(string text, int i, out MdSpan span, out int consumed)
    {
        span = new MdText("");
        consumed = 0;
        if (i + 3 > text.Length || text[i] != '*' || text[i + 1] != '*')
            return false;
        var end = text.IndexOf("**", i + 2, StringComparison.Ordinal);
        if (end < 0)
            return false;
        span = new MdBold(text[(i + 2)..end]);
        consumed = end + 2 - i;
        return true;
    }

    private static bool TryLink(string text, int i, out MdSpan span, out int consumed)
    {
        span = new MdText("");
        consumed = 0;
        if (text[i] != '[')
            return false;
        var close = text.IndexOf("](", i + 1, StringComparison.Ordinal);
        if (close < 0)
            return false;
        var end = text.IndexOf(')', close + 2);
        if (end < 0)
            return false;
        span = new MdLink(text[(i + 1)..close], text[(close + 2)..end]);
        consumed = end + 1 - i;
        return true;
    }
}
