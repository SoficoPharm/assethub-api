namespace AssetHub.Api.Authorization;

/// <summary>
/// The only role values stored in SystemUsers.Role.
/// </summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    /// <summary>IT support staff: can view, add and edit everything, but never delete.</summary>
    public const string SupportIT = "SupportIT";
    public const string Viewer = "Viewer";

    public static readonly string[] All = { SuperAdmin, Admin, SupportIT, Viewer };

    public static bool IsValid(string? role) => role != null && All.Contains(role);
}

public static class Policies
{
    public const string RequireSuperAdmin = "RequireSuperAdmin";
    /// <summary>Every POST/PUT outside system users (including assign, return, toggle-active): SuperAdmin, Admin, SupportIT.</summary>
    public const string CanCreateOrEdit = "CanCreateOrEdit";
    /// <summary>Every DELETE outside system users and the audit log: SuperAdmin, Admin.</summary>
    public const string CanDelete = "CanDelete";
}
