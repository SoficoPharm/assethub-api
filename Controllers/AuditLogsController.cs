using AssetHub.Api.Data;
using AssetHub.Api.Authorization;
using AssetHub.Api.DTOs;
using AssetHub.Api.Services;
using AssetHub.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AuditService _audit;

    public AuditLogsController(AppDbContext context, AuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? search,
        [FromQuery] string? action
    )
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();

            query = query.Where(x =>
                x.Actor.ToLower().Contains(keyword) ||
                x.LogAction.ToLower().Contains(keyword) ||
                x.Target.ToLower().Contains(keyword) ||
                x.Details.ToLower().Contains(keyword)
            );
        }

        if (!string.IsNullOrWhiteSpace(action) && action.ToLower() != "all")
        {
            var selectedAction = action.Trim().ToLower();
            query = query.Where(x => x.LogAction.ToLower() == selectedAction);
        }

        var logs = await query
            .OrderByDescending(x => x.Timestamp)
            .Select(x => new AuditLogDto
            {
                Id = x.Id,
                Timestamp = x.Timestamp,
                TimestampText = x.Timestamp.ToString("MMM dd, yyyy HH:mm"),
                Actor = x.Actor,
                LogAction = x.LogAction,
                Target = x.Target,
                Details = x.Details
            })
            .ToListAsync();

        return Ok(logs);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAuditLog(int id)
    {
        var log = await _context.AuditLogs
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new AuditLogDto
            {
                Id = x.Id,
                Timestamp = x.Timestamp,
                TimestampText = x.Timestamp.ToString("MMM dd, yyyy HH:mm"),
                Actor = x.Actor,
                LogAction = x.LogAction,
                Target = x.Target,
                Details = x.Details
            })
            .FirstOrDefaultAsync();

        if (log == null)
            return NotFound(new { message = "Audit log not found" });

        return Ok(log);
    }

    [Authorize(Policy = Policies.RequireSuperAdmin)]
    [HttpDelete("clear")]
    public async Task<IActionResult> ClearAuditLogs()
    {
        var logs = await _context.AuditLogs.ToListAsync();

        if (logs.Count == 0)
            return Ok(new { message = "No audit logs to clear" });

        _context.AuditLogs.RemoveRange(logs);

        // Leave a trace of who wiped the log.
        _audit.Log(AuditActions.Delete, "Audit Logs", $"Cleared {logs.Count} audit log entries");

        await _context.SaveChangesAsync();

        return Ok(new { message = "Audit logs cleared successfully" });
    }
}