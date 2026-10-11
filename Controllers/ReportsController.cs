using AssetHub.Api.Data;
using AssetHub.Api.DTOs;
using AssetHub.Api.Infrastructure;
using AssetHub.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReportsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetReports([FromQuery] string range = "This Year")
    {
        var devicesQuery = _context.Devices.AsNoTracking();

        if (range == "This Year")
        {
            // Calendar year in Egypt, not in UTC.
            var (startUtc, endUtc) = AppTime.CurrentYearUtc();
            devicesQuery = devicesQuery.Where(x => x.CreatedAt >= startUtc && x.CreatedAt < endUtc);
        }

        var totalDevices = await devicesQuery.CountAsync();

        var assigned = await devicesQuery
            .CountAsync(x => x.Status == DeviceStatuses.Assigned);

        var available = await devicesQuery
            .CountAsync(x => x.Status == DeviceStatuses.Available);

        var maintenance = await devicesQuery
            .CountAsync(x => x.Status == DeviceStatuses.Maintenance);

        var inactiveLost = await devicesQuery
            .CountAsync(x => x.Status == DeviceStatuses.Inactive || x.Status == DeviceStatuses.Lost);

        var devicesPerType = await devicesQuery
            .GroupBy(x => string.IsNullOrWhiteSpace(x.DeviceType) ? "N/A" : x.DeviceType)
            .Select(x => new ReportChartItemDto
            {
                Name = x.Key,
                Value = x.Count()
            })
            .OrderByDescending(x => x.Value)
            .ToListAsync();

        var devicesPerDepartment = await devicesQuery
            .Include(x => x.AssignedUser)
            .Where(x => x.AssignedUser != null)
            .GroupBy(x => string.IsNullOrWhiteSpace(x.AssignedUser!.Department) ? "N/A" : x.AssignedUser!.Department)
            .Select(x => new ReportChartItemDto
            {
                Name = x.Key,
                Value = x.Count()
            })
            .OrderByDescending(x => x.Value)
            .ToListAsync();

        var response = new ReportsResponseDto
        {
            DevicesPerType = devicesPerType,
            DevicesPerDepartment = devicesPerDepartment,
            AvailabilityStatus = new AvailabilityStatusDto
            {
                TotalDevices = totalDevices,
                Assigned = assigned,
                Available = available,
                Maintenance = maintenance,
                InactiveLost = inactiveLost
            }
        };

        return Ok(response);
    }
}