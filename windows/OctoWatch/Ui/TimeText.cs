using System.Globalization;

namespace OctoWatch;

/// <summary>Formats a GitHub ISO timestamp into a localized "x ago" label.</summary>
public static class TimeText
{
    public static string Ago(string iso)
    {
        if (
            string.IsNullOrWhiteSpace(iso)
            || !DateTimeOffset.TryParse(
                iso,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var then
            )
        )
            return "";

        var (unit, value) = RelativeTime.Bucket(DateTimeOffset.UtcNow, then);
        return unit switch
        {
            RelativeUnit.JustNow => Loc.Get("Time_JustNow"),
            RelativeUnit.Minutes => string.Format(Loc.Get("Time_Minutes"), value),
            RelativeUnit.Hours => string.Format(Loc.Get("Time_Hours"), value),
            RelativeUnit.Yesterday => Loc.Get("Time_Yesterday"),
            RelativeUnit.Days => string.Format(Loc.Get("Time_Days"), value),
            RelativeUnit.Weeks => string.Format(Loc.Get("Time_Weeks"), value),
            _ => "",
        };
    }
}
