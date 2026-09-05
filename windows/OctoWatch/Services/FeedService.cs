using uniffi.octowatch_core;

namespace OctoWatch;

internal static class FeedService
{
    public static List<Repo> ResolveRepos(string ownerText, string nameText)
    {
        var selected = SettingsStore.Load().SelectedRepos;
        if (selected.Count > 0)
        {
            var parsed = new List<Repo>();
            foreach (var fullName in selected)
            {
                if (FeedMapper.TryParseFullName(fullName, out var owner, out var name))
                    parsed.Add(new Repo(owner, name));
            }
            return parsed;
        }

        ownerText = ownerText.Trim();
        nameText = nameText.Trim();
        if (ownerText.Length == 0 || nameText.Length == 0)
            return [];
        return [new Repo(ownerText, nameText)];
    }

    public static FeedSnapshot FetchAll(IReadOnlyList<Repo> repos)
    {
        using var client = GitHubSession.CreateClient();
        var items = new List<FeedItem>();
        string? error = null;
        var authFailed = false;
        foreach (var repo in repos)
        {
            try
            {
                var full = FeedMapper.FullName(repo.owner, repo.name);
                items.AddRange(MapRuns(client.ListWorkflowRuns(repo), full));
                items.AddRange(MapPulls(client.ListPullRequests(repo), full));
                items.AddRange(MapBranches(client.ListBranches(repo), full));
            }
            catch (Exception ex) when (CoreError.IsAuth(ex))
            {
                authFailed = true;
                break; // the stored token is invalid; no point trying the rest
            }
            catch (Exception ex)
            {
                error = $"{FeedMapper.FullName(repo.owner, repo.name)}: {CoreError.Describe(ex)}";
            }
        }
        return new FeedSnapshot(items, error, authFailed);
    }

    private static IEnumerable<FeedItem> MapRuns(IEnumerable<WorkflowRun> runs, string full)
    {
        foreach (var run in runs)
        {
            var title = string.IsNullOrEmpty(run.name) ? run.commitMessage : run.name;
            yield return new FeedItem(
                FeedMapper.KindAction,
                "\uE9F5",
                title,
                $"{full} · {run.branch}",
                FeedMapper.MapRunState(run.status, run.conclusion),
                run.htmlUrl,
                full,
                run.id,
                UpdatedAt: run.updatedAt
            );
        }
    }

    private static IEnumerable<FeedItem> MapPulls(IEnumerable<PullRequest> pulls, string full)
    {
        foreach (var pr in pulls)
        {
            yield return new FeedItem(
                FeedMapper.KindPull,
                "\uE8A1",
                $"#{pr.number} {pr.title}",
                $"{full} · {pr.author} · {pr.headBranch} → {pr.baseBranch}",
                FeedMapper.MapPullState(pr.state, pr.draft, pr.merged),
                pr.htmlUrl,
                full,
                PullNumber: pr.number,
                UpdatedAt: pr.updatedAt
            );
        }
    }

    private static IEnumerable<FeedItem> MapBranches(IEnumerable<Branch> branches, string full)
    {
        foreach (var branch in branches)
        {
            var url =
                $"https://github.com/{full}/tree/{Uri.EscapeDataString(branch.name)}";
            var flag = branch.@protected
                ? "protected"
                : branch.lastCommitSha[..Math.Min(7, branch.lastCommitSha.Length)];
            yield return new FeedItem(
                FeedMapper.KindBranch,
                "\uE1D3",
                branch.name,
                $"{full} · {flag}",
                FeedMapper.MapBranchState(branch.@protected),
                url,
                full,
                BranchName: branch.name
            );
        }
    }
}

internal sealed record FeedSnapshot(List<FeedItem> Items, string? Error, bool AuthFailed = false);
