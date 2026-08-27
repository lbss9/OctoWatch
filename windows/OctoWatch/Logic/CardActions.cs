namespace OctoWatch;

public sealed record FeedItem(
    string Kind,
    string KindGlyph,
    string Title,
    string Subtitle,
    string State,
    string Url,
    string RepoFullName = "",
    long RunId = 0,
    long PullNumber = 0,
    string BranchName = "",
    DateTimeOffset? UpdatedAt = null
);

public sealed record CardAction(string Id, string LabelKey);

public static class CardActionCatalog
{
    public const string Open = "open";
    public const string Rerun = "rerun";
    public const string RerunFailed = "rerun_failed";
    public const string Cancel = "cancel";
    public const string OpenFiles = "open_files";
    public const string OpenChecks = "open_checks";
    public const string OpenCommits = "open_commits";

    public static IReadOnlyList<CardAction> For(string kind, string state)
    {
        var actions = new List<CardAction>
        {
            new(
                Open,
                kind == FeedMapper.KindPull ? "Card_OpenPr" : "Card_Open"
            ),
        };

        if (kind == FeedMapper.KindAction)
        {
            if (state == "running")
                actions.Add(new(Cancel, "Card_CancelRun"));
            else
            {
                actions.Add(new(Rerun, "Card_Rerun"));
                if (state == "failure")
                    actions.Add(new(RerunFailed, "Card_RerunFailed"));
            }
        }
        else if (kind == FeedMapper.KindPull)
        {
            actions.Add(new(OpenFiles, "Card_OpenFiles"));
            actions.Add(new(OpenChecks, "Card_OpenChecks"));
        }
        else if (kind == FeedMapper.KindBranch)
        {
            actions.Add(new(OpenCommits, "Card_OpenCommits"));
        }

        return actions;
    }
}
