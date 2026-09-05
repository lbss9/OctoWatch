using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace OctoWatch;

/// <summary>Maps a feed item's status to the colored accent used on its card.</summary>
public static class FeedTheme
{
    public static Brush AccentBrush(string state) =>
        new SolidColorBrush(
            state switch
            {
                "success" => Color.FromArgb(0xFF, 0x2E, 0xA0, 0x43), // green
                "failure" => Color.FromArgb(0xFF, 0xD6, 0x3B, 0x3B), // red
                "running" => Color.FromArgb(0xFF, 0xE3, 0xB3, 0x41), // amber
                _ => Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A), // gray
            }
        );
}
