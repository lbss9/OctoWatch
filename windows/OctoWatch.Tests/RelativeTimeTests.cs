using OctoWatch;
using Xunit;

namespace OctoWatch.Tests;

public class RelativeTimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(10, RelativeUnit.JustNow, 0)]
    [InlineData(90, RelativeUnit.Minutes, 1)]
    [InlineData(20 * 60, RelativeUnit.Minutes, 20)]
    [InlineData(3 * 3600, RelativeUnit.Hours, 3)]
    [InlineData(30 * 3600, RelativeUnit.Yesterday, 1)]
    [InlineData(3 * 86400, RelativeUnit.Days, 3)]
    [InlineData(20 * 86400, RelativeUnit.Weeks, 2)]
    public void Bucket_maps_spans(int secondsAgo, RelativeUnit unit, int value)
    {
        var (u, v) = RelativeTime.Bucket(Now, Now.AddSeconds(-secondsAgo));
        Assert.Equal(unit, u);
        Assert.Equal(value, v);
    }

    [Fact]
    public void Bucket_clamps_future_timestamps_to_just_now()
    {
        var (u, _) = RelativeTime.Bucket(Now, Now.AddMinutes(5));
        Assert.Equal(RelativeUnit.JustNow, u);
    }
}
