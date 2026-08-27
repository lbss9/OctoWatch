using System.Text.Json.Serialization;

namespace OctoWatch;

public sealed class AppSettings
{
    [JsonPropertyName("selectedRepos")]
    public List<string> SelectedRepos { get; set; } = [];

    [JsonPropertyName("globalEvents")]
    public List<string> GlobalEvents { get; set; } = [.. MonitorEvents.Default];

    [JsonPropertyName("eventsByRepo")]
    public Dictionary<string, List<string>> EventsByRepo { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("pollingSeconds")]
    public int PollingSeconds { get; set; } = 60;

    [JsonPropertyName("language")]
    public string Language { get; set; } = "pt-BR";

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "System";

    [JsonPropertyName("startWithWindows")]
    public bool StartWithWindows { get; set; }
}

public static class MonitorEvents
{
    public const string PrOpened = "pr_opened";
    public const string PrMerged = "pr_merged";
    public const string PrClosed = "pr_closed";
    public const string ReviewRequested = "review_requested";
    public const string Mention = "mention";
    public const string TeamMention = "team_mention";
    public const string CiActivity = "ci_activity";
    public const string Push = "push";
    public const string Assign = "assign";

    public static readonly string[] Default =
    [
        PrOpened,
        PrMerged,
        PrClosed,
        ReviewRequested,
        Mention,
        TeamMention,
        CiActivity,
        Push,
        Assign,
    ];

    public static IReadOnlyList<(string Id, string LabelKey)> Catalog { get; } =
        [
            (PrOpened, "Event_PrOpened"),
            (PrMerged, "Event_PrMerged"),
            (PrClosed, "Event_PrClosed"),
            (ReviewRequested, "Event_ReviewRequested"),
            (Mention, "Event_Mention"),
            (TeamMention, "Event_TeamMention"),
            (CiActivity, "Event_CiActivity"),
            (Push, "Event_Push"),
            (Assign, "Event_Assign"),
        ];
}
