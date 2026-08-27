using Velopack;
using Velopack.Sources;

namespace OctoWatch;

/// <summary>Checks GitHub Releases for a newer build and applies it via Velopack.</summary>
internal static class UpdateService
{
    private const string RepoUrl = "https://github.com/lbss9/octowatch";

    public static bool IsSupported
    {
        get
        {
            try
            {
                var manager = new UpdateManager(new GithubSource(RepoUrl, null, false));
                return manager.IsInstalled;
            }
            catch
            {
                return false;
            }
        }
    }

    public static async Task<bool> CheckAndApplyAsync()
    {
        try
        {
            var mgr = new UpdateManager(new GithubSource(RepoUrl, null, false));
            if (!mgr.IsInstalled)
                return false;
            var info = await mgr.CheckForUpdatesAsync();
            if (info is null)
                return false;
            await mgr.DownloadUpdatesAsync(info);
            mgr.ApplyUpdatesAndRestart(info);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
