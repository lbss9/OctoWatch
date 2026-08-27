using OctoWatch;
using Xunit;

namespace OctoWatch.Tests;

public class FeedMapperTests
{
    [Fact]
    public void MapRunState_in_progress_is_running()
    {
        Assert.Equal("running", FeedMapper.MapRunState("in_progress", null));
        Assert.Equal("running", FeedMapper.MapRunState("queued", null));
    }

    [Fact]
    public void MapRunState_maps_conclusions()
    {
        Assert.Equal("success", FeedMapper.MapRunState("completed", "success"));
        Assert.Equal("failure", FeedMapper.MapRunState("completed", "failure"));
        Assert.Equal("failure", FeedMapper.MapRunState("completed", "timed_out"));
        Assert.Equal("other", FeedMapper.MapRunState("completed", "cancelled"));
    }

    [Fact]
    public void MapPullState_open_merged_and_draft()
    {
        Assert.Equal("running", FeedMapper.MapPullState("open", false, false));
        Assert.Equal("other", FeedMapper.MapPullState("open", true, false));
        Assert.Equal("success", FeedMapper.MapPullState("closed", false, true));
        Assert.Equal("other", FeedMapper.MapPullState("closed", false, false));
    }

    [Fact]
    public void Visible_and_Cleared_respect_the_active_filter()
    {
        var items = new List<FeedItem>
        {
            new("action", "", "a", "", "success", ""),
            new("pr", "", "p", "", "running", ""),
            new("branch", "", "b", "", "other", ""),
        };

        Assert.Equal(3, FeedMapper.Visible(items, new FeedFilter()).Count);

        var branchesOnly = new FeedFilter
        {
            Actions = false,
            Pulls = false,
            Branches = true,
        };
        Assert.Single(FeedMapper.Visible(items, branchesOnly));
        Assert.Equal("b", FeedMapper.Visible(items, branchesOnly)[0].Title);

        var withoutBranches = FeedMapper.Cleared(items, branchesOnly);
        Assert.Equal(2, withoutBranches.Count);
        Assert.DoesNotContain(withoutBranches, i => i.Kind == FeedMapper.KindBranch);

        Assert.Empty(FeedMapper.Cleared(items, new FeedFilter()));
    }

    [Fact]
    public void Identity_uses_kind_repo_and_native_id()
    {
        var action = new FeedItem("action", "", "a", "", "success", "https://x", "o/r", 11);
        var pull = new FeedItem("pr", "", "p", "", "running", "https://y", "o/r", PullNumber: 7);
        var branch = new FeedItem(
            "branch",
            "",
            "main",
            "",
            "other",
            "https://z",
            "o/r",
            BranchName: "main"
        );

        Assert.Equal("a:o/r:11", FeedMapper.Identity(action));
        Assert.Equal("p:o/r:7", FeedMapper.Identity(pull));
        Assert.Equal("b:o/r:main", FeedMapper.Identity(branch));
    }

    [Fact]
    public void NewlyArrived_skips_seen_items_and_hidden_kinds()
    {
        var previous = new FeedItem("action", "", "old", "", "success", "", "o/r", 1);
        var nextAction = new FeedItem("action", "", "new", "", "failure", "", "o/r", 2);
        var nextPull = new FeedItem("pr", "", "pr", "", "running", "", "o/r", PullNumber: 3);
        var seen = new HashSet<string> { FeedMapper.Identity(previous) };
        var filter = new FeedFilter { Actions = true, Pulls = false, Branches = true };

        var fresh = FeedMapper.NewlyArrived([previous, nextAction, nextPull], seen, filter);

        Assert.Single(fresh);
        Assert.Equal("new", fresh[0].Title);
    }

    [Fact]
    public void ClearLabelKey_matches_single_kind_or_all()
    {
        Assert.Equal("Home_ClearAll", new FeedFilter().ClearLabelKey());
        Assert.Equal(
            "Home_ClearActions",
            new FeedFilter { Actions = true, Pulls = false, Branches = false }.ClearLabelKey()
        );
        Assert.Equal(
            "Home_ClearAll",
            new FeedFilter { Actions = true, Pulls = true, Branches = false }.ClearLabelKey()
        );
    }
}

public class CardActionCatalogTests
{
    [Fact]
    public void Failed_action_offers_rerun_and_rerun_failed()
    {
        var ids = CardActionCatalog.For(FeedMapper.KindAction, "failure").Select(a => a.Id).ToList();
        Assert.Contains(CardActionCatalog.Open, ids);
        Assert.Contains(CardActionCatalog.Rerun, ids);
        Assert.Contains(CardActionCatalog.RerunFailed, ids);
        Assert.DoesNotContain(CardActionCatalog.Cancel, ids);
    }

    [Fact]
    public void Running_action_offers_cancel_not_rerun()
    {
        var ids = CardActionCatalog.For(FeedMapper.KindAction, "running").Select(a => a.Id).ToList();
        Assert.Contains(CardActionCatalog.Cancel, ids);
        Assert.DoesNotContain(CardActionCatalog.Rerun, ids);
    }

    [Fact]
    public void Pull_request_offers_open_files_and_checks()
    {
        var ids = CardActionCatalog.For(FeedMapper.KindPull, "running").Select(a => a.Id).ToList();
        Assert.Contains(CardActionCatalog.Open, ids);
        Assert.Contains(CardActionCatalog.OpenFiles, ids);
        Assert.Contains(CardActionCatalog.OpenChecks, ids);
        Assert.DoesNotContain(CardActionCatalog.Rerun, ids);
    }
}

public class AppSettingsTests
{
    [Fact]
    public void Default_settings_include_all_monitor_events()
    {
        var settings = new AppSettings();
        Assert.Equal(MonitorEvents.Default, settings.GlobalEvents);
        Assert.Equal(60, settings.PollingSeconds);
        Assert.Equal("pt-BR", settings.Language);
        Assert.Equal("System", settings.Theme);
        Assert.False(settings.AutoUpdate);
    }
}

public class FeedDiffTests
{
    [Fact]
    public void Apply_inserts_updates_moves_and_removes()
    {
        var target = new System.Collections.ObjectModel.ObservableCollection<FeedItem>
        {
            Item("action", 1, "old"),
            Item("action", 2, "keep"),
            Item("pr", 3, "move-me"),
        };

        var desired = new List<FeedItem>
        {
            Item("pr", 3, "moved"),
            Item("action", 2, "keep"),
            Item("branch", 4, "new"),
        };

        FeedDiff.Apply(target, desired);

        Assert.Equal(3, target.Count);
        Assert.Equal("moved", target[0].Title);
        Assert.Equal("keep", target[1].Title);
        Assert.Equal("new", target[2].Title);
    }

    private static FeedItem Item(string kind, long id, string title) =>
        kind switch
        {
            "pr" => new FeedItem(kind, "", title, "", "running", "", "o/r", PullNumber: id),
            "branch" => new FeedItem(kind, "", title, "", "other", "", "o/r", BranchName: title),
            _ => new FeedItem(kind, "", title, "", "success", "", "o/r", id),
        };
}

public class RelativeTimeTests
{
    [Fact]
    public void Describe_uses_minutes_hours_yesterday_and_date()
    {
        var now = new DateTimeOffset(2026, 8, 27, 15, 0, 0, TimeSpan.Zero);

        Assert.Equal("Time_JustNow", RelativeTime.Describe(now.AddSeconds(-10), now).Key);
        Assert.Equal(5, RelativeTime.Describe(now.AddMinutes(-5), now).Count);
        Assert.Equal("Time_HoursAgo", RelativeTime.Describe(now.AddHours(-3), now).Key);
        Assert.Equal("Time_Yesterday", RelativeTime.Describe(now.AddDays(-1), now).Key);
        Assert.Equal("2026-08-01", RelativeTime.Describe(now.AddDays(-26), now).Date);
    }
}

public class MarkdownLiteTests
{
    [Fact]
    public void Parse_headings_bullets_bold_and_links()
    {
        var blocks = MarkdownLite.Parse(
            """
            # Title
            ## Section
            A **bold** [link](https://example.com) here.
            - one
            - two
            """
        );

        Assert.Equal(4, blocks.Count);
        Assert.IsType<MdHeading>(blocks[0]);
        Assert.Equal(1, ((MdHeading)blocks[0]).Level);
        Assert.IsType<MdHeading>(blocks[1]);
        var paragraph = Assert.IsType<MdParagraph>(blocks[2]);
        Assert.Contains(paragraph.Spans, s => s is MdBold);
        Assert.Contains(paragraph.Spans, s => s is MdLink link && link.Url.StartsWith("https://"));
        var list = Assert.IsType<MdList>(blocks[3]);
        Assert.Equal(2, list.Items.Count);
    }
}
