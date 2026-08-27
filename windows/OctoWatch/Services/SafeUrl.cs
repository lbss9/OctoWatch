using Windows.System;

namespace OctoWatch;

/// <summary>
/// Opens external links, but only over http/https. Feed data comes from the
/// GitHub API, so this guards against a crafted value launching some other
/// protocol handler (file:, ms-*:, custom schemes) when a link is clicked.
/// </summary>
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
