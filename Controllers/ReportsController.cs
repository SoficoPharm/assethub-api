using AssetHub.Api.Data;
using AssetHub.Api.DTOs;
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
            var year = DateTime.Now.Year;
            devicesQuery = devicesQuery.Where(x => x.CreatedAt.Year == year);
        }

        var totalDevices = await devicesQuery.CountAsync();

        var assigned = await devicesQuery
            .CountAsync(x => x.Status == "Assigned" || x.Status == "Active");

        var available = await devicesQuery
            .CountAsync(x => x.Status == "Available");

        var maintenance = await devicesQuery
            .CountAsync(x => x.Status == "Maintenance");

        var inactiveLost = await devicesQuery
            .CountAsync(x => x.Status == "Inactive" || x.Status == "Lost");

        var devicesPerType = await devicesQuery
            .GroupBy(x => string.IsNullOrWhiteSpace(x.DeviceType) ? "N/A" : x.DeviceType)
            .Select(x => new ReportChartItemDto
            {
                Name = x.Key,
                Value = x.Count()
            })
            .OrderByDescending(x => x.Value)
            .ToListAsync();

        var devicesPerDepartment = await _context.Devices
            .AsNoTracking()
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