using Microsoft.EntityFrameworkCore;

namespace AssetHub.Api.Services;

/// <summary>
/// Generates sequential codes such as DEV-0001 from the highest existing number,
/// so deleted rows never cause a code to be reused or collide.
/// </summary>
public static class CodeGenerator
{
    public static async Task<string> NextAsync(IQueryable<string> existingCodes, string prefix, int digits)
    {
        var codes = await existingCodes
            .Where(code => code.StartsWith(prefix))
            .ToListAsync();

        var max = codes
            .Select(code => int.TryParse(code.AsSpan(prefix.Length), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        return prefix + (max + 1).ToString(new string('0', digits));
    }
}
