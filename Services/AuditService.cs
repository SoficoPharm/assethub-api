using AssetHub.Api.Data;
using AssetHub.Api.Models;

namespace AssetHub.Api.Services;

public static class AuditActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string Assign = "Assign";
    public const string Return = "Return";
    public const string Alert = "Alert";
}

/// <summary>
/// Adds audit entries to the current DbContext. The entry is saved together with
/// the caller's own changes when the caller calls SaveChangesAsync.
/// </summary>
public class AuditService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUser _currentUser;

    public AuditService(AppDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public void Log(string action, string target, string details)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            Actor = _currentUser.DisplayName,
            LogAction = action,
            Target = target,
            Details = details
        });
    }
}
