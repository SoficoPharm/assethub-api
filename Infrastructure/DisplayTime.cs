namespace AssetHub.Api.Infrastructure;

/// <summary>
/// Timestamps are stored in UTC. Pre-formatted display strings are rendered in the
/// server's local time zone (the API and its users are on the same site).
/// </summary>
public static class DisplayTime
{
    public static string Format(DateTime utc, string format) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.Local)
            .ToString(format);

    public static string? Format(DateTime? utc, string format) =>
        utc == null ? null : Format(utc.Value, format);
}
