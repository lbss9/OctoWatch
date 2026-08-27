namespace OctoWatch;

public readonly record struct RelativeTimeParts(string Key, int? Count, string? Date = null);

/// <summary>Turns an instant into a short relative label key for the UI to localize.</summary>
public static class RelativeTime
{
    public static RelativeTimeParts Describe(DateTimeOffset when, DateTimeOffset now)
    {
        var span = now - when;
        if (span.TotalSeconds < 45)
            return new("Time_JustNow", null);
        if (span.TotalMinutes < 60)
            return new("Time_MinutesAgo", Math.Max(1, (int)span.TotalMinutes));
        if (span.TotalHours < 24)
            return new("Time_HoursAgo", Math.Max(1, (int)span.TotalHours));
        if (when.Date == now.Date.AddDays(-1))
            return new("Time_Yesterday", null);
        if (span.TotalDays < 7)
            return new("Time_DaysAgo", Math.Max(1, (int)span.TotalDays));
        return new("Time_OnDate", null, when.UtcDateTime.ToString("yyyy-MM-dd"));
    }
}
