using Windows.System;

namespace OctoWatch;

/// <summary>Opens http(s) URLs only — blocks file, javascript, and other schemes.</summary>
internal static class SafeUrl
{
    public static async Task<bool> OpenAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;
        return await Launcher.LaunchUriAsync(uri);
    }
}
