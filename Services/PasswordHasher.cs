namespace AssetHub.Api.Services;

public static class PasswordHasher
{
    public const int MinLength = 8;

    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public static bool IsHash(string? stored) =>
        stored is { Length: 60 } &&
        (stored.StartsWith("$2a$") || stored.StartsWith("$2b$") || stored.StartsWith("$2y$"));

    /// <summary>
    /// Verifies a password. Legacy rows still hold plaintext; those are compared
    /// directly and <paramref name="needsRehash"/> is set so the caller can upgrade them.
    /// </summary>
    public static bool Verify(string password, string stored, out bool needsRehash)
    {
        if (IsHash(stored))
        {
            needsRehash = false;
            return BCrypt.Net.BCrypt.Verify(password, stored);
        }

        needsRehash = true;
        return stored == password;
    }
}
