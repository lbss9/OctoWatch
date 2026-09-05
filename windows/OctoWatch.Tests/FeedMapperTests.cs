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
    }
}
