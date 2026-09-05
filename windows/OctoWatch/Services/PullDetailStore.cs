using uniffi.octowatch_core;

namespace OctoWatch;

/// <summary>
/// Small in-memory cache for on-demand pull-request details, so re-expanding a
/// card (or expanding it again within a minute) is instant and doesn't spend an
/// API request. This is deliberately simple; a real ETag/conditional-request
/// layer for the whole feed is a separate, bigger improvement.
/// </summary>
internal static class PullDetailStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private static readonly Dictionary<string, (DateTimeOffset At, PullDetail Detail)> Cache = new();
    private static readonly object Gate = new();

    public static async Task<PullDetail> GetAsync(string owner, string name, long number)
    {
        var key = $"{owner}/{name}#{number}";
        lock (Gate)
        {
            if (Cache.TryGetValue(key, out var hit) && DateTimeOffset.UtcNow - hit.At < Ttl)
                return hit.Detail;
        }

        var detail = await Task.Run(() =>
        {
            using var client = GitHubSession.CreateClient();
            return client.GetPullRequest(new Repo(owner, name), number);
        });

        lock (Gate)
        {
            Cache[key] = (DateTimeOffset.UtcNow, detail);
        }
        return detail;
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Cache.Clear();
        }
    }
}
