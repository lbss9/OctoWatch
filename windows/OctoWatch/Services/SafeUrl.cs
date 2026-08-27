using Windows.System;

namespace OctoWatch;

/// <summary>Opens http(s) URLs only — blocks file, javascript, and other schemes.</summary>
internal static class SafeUrl
{
    public static async Task OpenAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;
        if (uri.Scheme is not "http" and not "https")
            return;
        await Launcher.LaunchUriAsync(uri);
    }
}
