using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace OctoWatch;

public static class CardAccent
{
    public static Brush For(string state) => new SolidColorBrush(ColorFor(state));

    public static Color ColorFor(string state) =>
        state switch
        {
            "success" => Color.FromArgb(0xFF, 0x2E, 0xA0, 0x43),
            "failure" => Color.FromArgb(0xFF, 0xD6, 0x3B, 0x3B),
            "running" => Color.FromArgb(0xFF, 0xE3, 0xB3, 0x41),
            _ => Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A),
        };
}
