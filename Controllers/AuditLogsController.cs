using AssetHub.Api.Data;
using AssetHub.Api.DTOs;
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

    public AuditLogsController(AppDbContext context)
    {
        _context = context;
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

    [HttpPost]
    public async Task<IActionResult> CreateAuditLog([FromBody] CreateAuditLogDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Actor))
            return BadRequest(new { message = "Actor is required" });

        if (string.IsNullOrWhiteSpace(dto.LogAction))
            return BadRequest(new { message = "Action is required" });

        var log = new AuditLog
        {
            Timestamp = DateTime.Now,
            Actor = dto.Actor.Trim(),
            LogAction = dto.LogAction.Trim(),
            Target = dto.Target?.Trim() ?? "",
            Details = dto.Details?.Trim() ?? ""
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();

        return Ok(new AuditLogDto
        {
            Id = log.Id,
            Timestamp = log.Timestamp,
            TimestampText = log.Timestamp.ToString("MMM dd, yyyy HH:mm"),
            Actor = log.Actor,
            LogAction = log.LogAction,
            Target = log.Target,
            Details = log.Details
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAuditLog(int id)
    {
        var log = await _context.AuditLogs.FindAsync(id);

        if (log == null)
            return NotFound(new { message = "Audit log not found" });

        _context.AuditLogs.Remove(log);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Audit log deleted successfully" });
    }

    [HttpDelete("clear")]
    public async Task<IActionResult> ClearAuditLogs()
    {
        var logs = await _context.AuditLogs.ToListAsync();

        if (logs.Count == 0)
            return Ok(new { message = "No audit logs to clear" });

        _context.AuditLogs.RemoveRange(logs);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Audit logs cleared successfully" });
    }
}