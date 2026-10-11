using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Api.Services;

/// <summary>
/// Device serial numbers are unique (filtered unique index IX_Devices_SerialNumber).
/// They are stored normalized so "cl7wnl3 " and "CL7WNL3" are the same serial.
/// </summary>
public static class SerialNumbers
{
    public const string UniqueIndexName = "IX_Devices_SerialNumber";

    /// <summary>Trims, upper-cases, and turns empty into null (no serial).</summary>
    public static string? Normalize(string? serial)
    {
        var trimmed = serial?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.ToUpperInvariant();
    }

    public static string ConflictMessage(string serial, string deviceCode, string deviceName) =>
        $"Serial number {serial} already exists on device {deviceCode} ({deviceName})";

    /// <summary>True when the save failed on the serial-number unique index (e.g. two requests raced).</summary>
    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 } sql &&
        sql.Message.Contains(UniqueIndexName, StringComparison.OrdinalIgnoreCase);
}
