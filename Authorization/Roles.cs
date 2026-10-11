namespace AssetHub.Api.Authorization;

/// <summary>
/// The only role values stored in SystemUsers.Role.
/// </summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Viewer = "Viewer";

    public static readonly string[] All = { SuperAdmin, Admin, Viewer };

    public static bool IsValid(string? role) => role != null && All.Contains(role);
}

public static class Policies
{
    public const string RequireSuperAdmin = "RequireSuperAdmin";
    public const string CanEdit = "CanEdit";
}
