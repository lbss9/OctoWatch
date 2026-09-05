namespace OctoWatch;

public enum RelativeUnit
{
    JustNow,
    Minutes,
    Hours,
    Yesterday,
    Days,
    Weeks,
}

/// <summary>
/// Pure bucketing for "how long ago" labels. The UI layer turns the bucket into
/// a localized string; keeping this side free of formatting makes it testable.
/// </summary>
public static class RelativeTime
{
    public static (RelativeUnit Unit, int Value) Bucket(DateTimeOffset now, DateTimeOffset then)
    {
        var span = now - then;
        if (span < TimeSpan.Zero)
            span = TimeSpan.Zero;

        if (span.TotalSeconds < 45)
            return (RelativeUnit.JustNow, 0);
        if (span.TotalMinutes < 60)
            return (RelativeUnit.Minutes, Math.Max(1, (int)span.TotalMinutes));
        if (span.TotalHours < 24)
            return (RelativeUnit.Hours, (int)span.TotalHours);
        if (span.TotalHours < 48)
            return (RelativeUnit.Yesterday, 1);
        if (span.TotalDays < 7)
            return (RelativeUnit.Days, (int)span.TotalDays);
        return (RelativeUnit.Weeks, Math.Max(1, (int)(span.TotalDays / 7)));
    }
}
