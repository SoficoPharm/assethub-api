namespace AssetHub.Api.Infrastructure;

/// <summary>
/// Timestamps are stored and returned in UTC; the frontend formats them in Egypt time.
/// The server only needs the local zone for calendar boundaries such as "this year".
/// </summary>
public static class AppTime
{
    public static readonly TimeZoneInfo Zone = FindZone();

    /// <summary>UTC range [start, end) covering the current calendar year in Egypt.</summary>
    public static (DateTime StartUtc, DateTime EndUtc) CurrentYearUtc()
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
        var start = new DateTime(localNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        return (
            TimeZoneInfo.ConvertTimeToUtc(start, Zone),
            TimeZoneInfo.ConvertTimeToUtc(start.AddYears(1), Zone));
    }

    private static TimeZoneInfo FindZone()
    {
        // Windows id first, IANA id for Linux hosts.
        foreach (var id in new[] { "Egypt Standard Time", "Africa/Cairo" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
                return zone;
        }

        throw new InvalidOperationException("Egypt time zone not found on this host.");
    }
}
