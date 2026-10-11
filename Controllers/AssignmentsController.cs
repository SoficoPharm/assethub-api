using AssetHub.Api.Authorization;
using AssetHub.Api.Data;
using AssetHub.Api.DTOs;
using AssetHub.Api.Infrastructure;
using AssetHub.Api.Models;
using AssetHub.Api.Services;
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
    private readonly AuditService _audit;

    public AssignmentsController(AppDbContext context, AuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAssignments()
    {
        var rows = await _context.Assignments
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Device)
            .OrderByDescending(x => x.Id)
            .ToListAsync();

        var data = rows.Select(x => new
        {
            dbId = x.Id,
            id = x.AssignmentCode,
            user = x.User.FullName,
            initials = GetInitials(x.User.FullName),
            device = x.Device.DeviceName,
            model = x.Device.Model,
            assignedDate = x.AssignedDate,
            returnedDate = x.ReturnedDate,
            status = x.Status
        });

        return Ok(data);
    }

    [Authorize(Policy = Policies.CanEdit)]
    [HttpPost]
    public async Task<IActionResult> AssignDevice([FromBody] AssignDeviceRequest request)
    {
        var user = await _context.Users.FindAsync(request.UserId);
        var device = await _context.Devices.FindAsync(request.DeviceId);

        if (user == null)
            return NotFound(new { message = "Employee not found" });

        if (device == null)
            return NotFound(new { message = "Device not found" });

        if (device.Status != DeviceStatuses.Available)
            return BadRequest(new { message = "Device is not available" });

        var activeAssignmentExists = await _context.Assignments
            .AnyAsync(x => x.DeviceId == device.Id && x.Status == AssignmentStatuses.Active);

        if (activeAssignmentExists)
            return BadRequest(new { message = "This device already has an active assignment" });

        device.Status = DeviceStatuses.Assigned;
        device.AssignedUserId = user.Id;

        var assignment = new Assignment
        {
            AssignmentCode = await CodeGenerator.NextAsync(_context.Assignments.Select(x => x.AssignmentCode), "ASN-", 3),
            UserId = user.Id,
            DeviceId = device.Id,
            AssignedDate = DateTime.UtcNow,
            Status = AssignmentStatuses.Active
        };

        _context.Assignments.Add(assignment);

        _audit.Log(AuditActions.Assign, $"{user.FullName} ({user.EmployeeId})", $"Assigned {device.DeviceName} ({device.DeviceCode})");

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Device assigned successfully",
            assignment.AssignmentCode
        });
    }

    [Authorize(Policy = Policies.CanEdit)]
    [HttpPut("{id:int}/return")]
    public async Task<IActionResult> ReturnDevice(int id)
    {
        var assignment = await _context.Assignments
            .Include(x => x.Device)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (assignment == null)
            return NotFound(new { message = "Assignment not found" });

        if (assignment.Status == AssignmentStatuses.Returned)
            return BadRequest(new { message = "Assignment already returned" });

        assignment.Status = AssignmentStatuses.Returned;
        assignment.ReturnedDate = DateTime.UtcNow;

        assignment.Device.Status = DeviceStatuses.Available;
        assignment.Device.AssignedUserId = null;

        _audit.Log(AuditActions.Return, $"{assignment.User.FullName} ({assignment.User.EmployeeId})", $"Returned {assignment.Device.DeviceName} ({assignment.Device.DeviceCode})");

        await _context.SaveChangesAsync();

        return Ok(new { message = "Device returned successfully" });
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
