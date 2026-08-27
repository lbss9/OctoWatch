namespace OctoWatch;

public static class FeedMapper
{
    public const string KindAction = "action";
    public const string KindPull = "pr";
    public const string KindBranch = "branch";

    public static string MapRunState(string status, string? conclusion)
    {
        if (status != "completed")
            return "running";
        return conclusion switch
        {
            "success" => "success",
            "failure" or "timed_out" or "startup_failure" or "action_required" => "failure",
            _ => "other",
        };
    }

    public static string MapPullState(string state, bool draft, bool merged)
    {
        if (merged)
            return "success";
        if (state == "open")
            return draft ? "other" : "running";
        return "other";
    }

    public static string MapBranchState(bool isProtected) => isProtected ? "other" : "success";

    public static string FullName(string owner, string name) => $"{owner}/{name}";

    public static bool TryParseFullName(string fullName, out string owner, out string name)
    {
        owner = "";
        name = "";
        var parts = fullName.Split(
            '/',
            2,
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
        );
        if (parts.Length != 2)
            return false;
        owner = parts[0];
        name = parts[1];
        return owner.Length > 0 && name.Length > 0;
    }

    public static IReadOnlyList<FeedItem> Visible(IEnumerable<FeedItem> items, FeedFilter filter) =>
        items.Where(item => filter.Allows(item.Kind)).ToList();

    public static List<FeedItem> Cleared(IEnumerable<FeedItem> items, FeedFilter filter)
    {
        if (filter.IsAll)
            return [];
        return items.Where(item => !filter.Allows(item.Kind)).ToList();
    }

    public static string Identity(FeedItem item)
    {
        if (item.Kind == KindAction && item.RunId != 0)
            return $"a:{item.RepoFullName}:{item.RunId}";
        if (item.Kind == KindPull && item.PullNumber != 0)
            return $"p:{item.RepoFullName}:{item.PullNumber}";
        if (item.Kind == KindBranch && item.BranchName.Length > 0)
            return $"b:{item.RepoFullName}:{item.BranchName}";
        return $"u:{item.Url}";
    }

    public static List<FeedItem> NewlyArrived(
        IEnumerable<FeedItem> items,
        ISet<string> seen,
        FeedFilter filter
    ) => items.Where(item => !seen.Contains(Identity(item)) && filter.Allows(item.Kind)).ToList();
}

public sealed class FeedFilter
{
    public bool Actions { get; set; } = true;
    public bool Pulls { get; set; } = true;
    public bool Branches { get; set; } = true;

    public bool IsAll => Actions && Pulls && Branches;

    public bool Allows(string kind) =>
        kind switch
        {
            FeedMapper.KindAction => Actions,
            FeedMapper.KindPull => Pulls,
            FeedMapper.KindBranch => Branches,
            _ => true,
        };

    public string ClearLabelKey()
    {
        var enabled = (Actions ? 1 : 0) + (Pulls ? 1 : 0) + (Branches ? 1 : 0);
        if (enabled != 1)
            return "Home_ClearAll";
        if (Actions)
            return "Home_ClearActions";
        if (Pulls)
            return "Home_ClearPrs";
        return "Home_ClearBranches";
    }
}
