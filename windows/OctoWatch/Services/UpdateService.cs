using Velopack;
using Velopack.Sources;

namespace OctoWatch;

internal enum UpdateResult
{
    NotInstalled,
    UpToDate,
    Updating,
    Failed,
}

/// <summary>
/// Checks GitHub Releases for a newer build and applies it via Velopack. The app
/// is unpackaged (no store identity), so it can't use App Installer's auto-update.
/// </summary>
internal static class UpdateService
{
    // TODO: point this at the real repository once OctoWatch is published.
    private const string RepoUrl = "https://github.com/OWNER/octowatch";

    /// <summary>
    /// Looks for an update and, if found, downloads it and restarts into it.
    /// Returns <see cref="UpdateResult.NotInstalled"/> for dev/unpackaged runs
    /// (nothing to update), and never throws.
    /// </summary>
    public static async Task<UpdateResult> CheckAndApplyAsync()
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(RepoUrl, null, prerelease: false));
            if (!manager.IsInstalled)
                return UpdateResult.NotInstalled;

            var update = await manager.CheckForUpdatesAsync();
            if (update is null)
                return UpdateResult.UpToDate;

            await manager.DownloadUpdatesAsync(update);
            manager.ApplyUpdatesAndRestart(update);
            return UpdateResult.Updating;
        }
        catch
        {
            return UpdateResult.Failed;
        }
    }
}
