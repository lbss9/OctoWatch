using System.Collections.ObjectModel;
using OctoWatch;
using Xunit;

namespace OctoWatch.Tests;

public class FeedDiffTests
{
    private static FeedItem Action(string title, long runId, string state = "success") =>
        new("action", "", title, state, state, "https://x", "o/r", runId);

    [Fact]
    public void Apply_adds_removes_and_reorders()
    {
        var target = new ObservableCollection<FeedItem>();

        FeedDiff.Apply(target, [Action("a", 1), Action("b", 2), Action("c", 3)]);
        Assert.Equal(new[] { 1L, 2L, 3L }, target.Select(Id));

        // Remove b, append d.
        FeedDiff.Apply(target, [Action("a", 1), Action("c", 3), Action("d", 4)]);
        Assert.Equal(new[] { 1L, 3L, 4L }, target.Select(Id));

        // Reverse order.
        FeedDiff.Apply(target, [Action("d", 4), Action("c", 3), Action("a", 1)]);
        Assert.Equal(new[] { 4L, 3L, 1L }, target.Select(Id));
    }

    [Fact]
    public void Apply_preserves_instances_for_unchanged_items()
    {
        var target = new ObservableCollection<FeedItem>();
        FeedDiff.Apply(target, [Action("a", 1), Action("b", 2)]);
        var a = target[0];
        var b = target[1];

        // Same content again: the diff must not replace the existing instances,
        // so ListView containers (and the pulse animation) are preserved.
        FeedDiff.Apply(target, [Action("a", 1), Action("b", 2)]);
        Assert.Same(a, target[0]);
        Assert.Same(b, target[1]);
    }

    [Fact]
    public void Apply_replaces_only_the_item_whose_content_changed()
    {
        var target = new ObservableCollection<FeedItem>();
        FeedDiff.Apply(target, [Action("a", 1, "running"), Action("b", 2)]);
        var b = target[1];

        // Same identity for run 1 but a new state -> that item is replaced, b is not.
        FeedDiff.Apply(target, [Action("a", 1, "success"), Action("b", 2)]);
        Assert.Equal("success", target[0].State);
        Assert.Same(b, target[1]);
    }

    private static long Id(FeedItem item) => item.RunId;
}
