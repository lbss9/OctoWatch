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
        foreach (var repo in repos)
        {
            try
            {
                var full = FeedMapper.FullName(repo.owner, repo.name);
                items.AddRange(MapRuns(client.ListWorkflowRuns(repo), full));
                items.AddRange(MapPulls(client.ListPullRequests(repo), full));
                items.AddRange(MapBranches(client.ListBranches(repo), full));
            }
            catch (Exception ex)
            {
                error = $"{FeedMapper.FullName(repo.owner, repo.name)}: {ex.Message}";
            }
        }
        return new FeedSnapshot(items, error);
    }

    private static IEnumerable<FeedItem> MapRuns(IEnumerable<WorkflowRun> runs, string full)
    {
        foreach (var run in runs)
        {
            var title = string.IsNullOrEmpty(run.name) ? run.commitMessage : run.name;
            var detail = run.conclusion ?? run.status;
            var when = ParseTime(run.updatedAt);
            yield return new FeedItem(
                FeedMapper.KindAction,
                "\uE9F5",
                title,
                Stamp(when, $"{full} · {run.branch} · {detail}"),
                FeedMapper.MapRunState(run.status, run.conclusion),
                run.htmlUrl,
                full,
                run.id,
                UpdatedAt: when
            );
        }
    }

    private static IEnumerable<FeedItem> MapPulls(IEnumerable<PullRequest> pulls, string full)
    {
        foreach (var pr in pulls)
        {
            var state = pr.merged ? "merged" : pr.state;
            var when = ParseTime(pr.updatedAt);
            yield return new FeedItem(
                FeedMapper.KindPull,
                "\uE8A1",
                $"#{pr.number} {pr.title}",
                Stamp(
                    when,
                    $"{full} · {pr.author} · {state} · {pr.headBranch} → {pr.baseBranch}"
                ),
                FeedMapper.MapPullState(pr.state, pr.draft, pr.merged),
                pr.htmlUrl,
                full,
                PullNumber: pr.number,
                UpdatedAt: when
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

    private static DateTimeOffset? ParseTime(string? iso) =>
        DateTimeOffset.TryParse(iso, out var when) ? when : null;

    private static string Stamp(DateTimeOffset? when, string rest)
    {
        if (when is null)
            return rest;
        var parts = RelativeTime.Describe(when.Value, DateTimeOffset.UtcNow);
        string label;
        if (parts.Date is not null)
            label = string.Format(Loc.Get(parts.Key), parts.Date);
        else if (parts.Count is int count)
            label = string.Format(Loc.Get(parts.Key), count);
        else
            label = Loc.Get(parts.Key);
        return $"{label} · {rest}";
    }
}

internal sealed record FeedSnapshot(List<FeedItem> Items, string? Error);
