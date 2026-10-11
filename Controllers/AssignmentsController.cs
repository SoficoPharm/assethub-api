using AssetHub.Api.Data;
using AssetHub.Api.Authorization;
using AssetHub.Api.DTOs;
using AssetHub.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssignmentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AssignmentsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAssignments()
    {
        var data = await _context.Assignments
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Device)
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                dbId = x.Id,
                id = x.AssignmentCode,
                user = x.User.FullName,
                initials = GetInitials(x.User.FullName),
                device = x.Device.DeviceName,
                model = x.Device.Model,
                assignedDate = x.AssignedDate.ToString("MMM dd, yyyy"),
                returnedDate = x.ReturnedDate == null ? "—" : x.ReturnedDate.Value.ToString("MMM dd, yyyy"),
                assignedDateRaw = x.AssignedDate,
                returnedDateRaw = x.ReturnedDate,
                status = x.Status
            })
            .ToListAsync();

        return Ok(data);
    }

    [Authorize(Policy = Policies.CanEdit)]
    [HttpPost]
    public async Task<IActionResult> AssignDevice([FromBody] AssignDeviceRequest request)
    {
        try
        {
            var user = await _context.Users.FindAsync(request.UserId);
            var device = await _context.Devices.FindAsync(request.DeviceId);

            if (user == null)
                return NotFound(new { message = "User not found" });

            if (device == null)
                return NotFound(new { message = "Device not found" });

            if (device.Status != "Available")
                return BadRequest(new { message = "Device is not available" });

            var activeAssignmentExists = await _context.Assignments
                .AnyAsync(x => x.DeviceId == device.Id && x.Status == "Active");

            if (activeAssignmentExists)
                return BadRequest(new { message = "This device already has an active assignment" });

            device.Status = "Assigned";
            device.AssignedUserId = user.Id;

            var assignment = new Assignment
            {
                AssignmentCode = await GenerateAssignmentCode(),
                UserId = user.Id,
                DeviceId = device.Id,
                AssignedDate = DateTime.Now,
                Status = "Active"
            };

            _context.Assignments.Add(assignment);

            _context.AuditLogs.Add(new AuditLog
            {
                Actor = "Admin User",
                LogAction = "Assign",
                Target = $"{user.FullName} ({user.EmployeeId})",
                Details = $"Assigned {device.DeviceName} ({device.DeviceCode})",
                Timestamp = DateTime.Now
            });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Device assigned successfully",
                assignment.AssignmentCode
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Policy = Policies.CanEdit)]
    [HttpPut("{id:int}/return")]
    public async Task<IActionResult> ReturnDevice(int id)
    {
        try
        {
            var assignment = await _context.Assignments
                .Include(x => x.Device)
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (assignment == null)
                return NotFound(new { message = "Assignment not found" });

            if (assignment.Status == "Returned")
                return BadRequest(new { message = "Assignment already returned" });

            assignment.Status = "Returned";
            assignment.ReturnedDate = DateTime.Now;

            assignment.Device.Status = "Available";
            assignment.Device.AssignedUserId = null;

            _context.AuditLogs.Add(new AuditLog
            {
                Actor = "Admin User",
                LogAction = "Return",
                Target = $"{assignment.User.FullName} ({assignment.User.EmployeeId})",
                Details = $"Returned {assignment.Device.DeviceName} ({assignment.Device.DeviceCode})",
                Timestamp = DateTime.Now
            });

            await _context.SaveChangesAsync();

            return Ok(new { message = "Device returned successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<string> GenerateAssignmentCode()
    {
        var count = await _context.Assignments.CountAsync();
        return $"ASN-{count + 1:000}";
    }

    private static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "";

        var initials = string.Join("", parts.Select(x => x[0]));

        return initials.Length >= 2
            ? initials.Substring(0, 2).ToUpper()
            : initials.ToUpper();
    }
}