using AssetHub.Api.Data;
using AssetHub.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var totalDevices = await _context.Devices
            .AsNoTracking()
            .CountAsync();

        var statusCounts = await _context.Devices
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                Status = g.Key ?? "N/A",
                Count = g.Count()
            })
            .ToListAsync();

        int GetStatusCount(params string[] statuses)
        {
            return statusCounts
                .Where(x => statuses.Contains(x.Status))
                .Sum(x => x.Count);
        }

        var assigned = GetStatusCount(DeviceStatuses.Assigned);
        var available = GetStatusCount(DeviceStatuses.Available);
        var maintenance = GetStatusCount(DeviceStatuses.Maintenance);
        var inactiveLost = GetStatusCount(DeviceStatuses.Inactive, DeviceStatuses.Lost);

        var totalUsers = await _context.Users
            .AsNoTracking()
            .CountAsync();

        var devicesByType = await _context.Devices
            .AsNoTracking()
            .GroupBy(x => string.IsNullOrWhiteSpace(x.DeviceType) ? "N/A" : x.DeviceType)
            .Select(g => new
            {
                name = g.Key,
                value = g.Count()
            })
            .ToListAsync();

        var devicesByPlatform = await _context.Devices
            .AsNoTracking()
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Platform) ? "N/A" : x.Platform)
            .Select(g => new
            {
                name = g.Key,
                value = g.Count()
            })
            .ToListAsync();

        var recentActivity = await _context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.Timestamp)
            .Take(5)
            .Select(x => new
            {
                title = x.Details,
                user = x.Actor,
                timestamp = x.Timestamp,
                color = x.LogAction == "Assign" ? "green"
                    : x.LogAction == "Return" ? "blue"
                    : x.LogAction == "Create" ? "purple"
                    : x.LogAction == "Delete" ? "red"
                    : "amber"
            })
            .ToListAsync();

        return Ok(new
        {
            statCards = new[]
            {
                new { label = "Total Devices", value = totalDevices.ToString("N0"), icon = "devices", iconClass = "blue" },
                new { label = "Assigned", value = assigned.ToString("N0"), icon = "check", iconClass = "green" },
                new { label = "Available", value = available.ToString("N0"), icon = "clipboard", iconClass = "gray" },
                new { label = "Maintenance", value = maintenance.ToString("N0"), icon = "tool", iconClass = "orange" },
                new { label = "Inactive/Lost", value = inactiveLost.ToString("N0"), icon = "warning", iconClass = "amber" },
                new { label = "Total Employees", value = totalUsers.ToString("N0"), icon = "users", iconClass = "purple" }
            },
            devicesByType,
            devicesByPlatform,
            recentActivity
        });
    }
}