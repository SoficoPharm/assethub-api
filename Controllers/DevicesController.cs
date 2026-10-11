using AssetHub.Api.Data;
using AssetHub.Api.Authorization;
using AssetHub.Api.DTOs;
using AssetHub.Api.Infrastructure;
using AssetHub.Api.Services;
using AssetHub.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AuditService _audit;
    private readonly IWebHostEnvironment _environment;

    public DevicesController(AppDbContext context, IWebHostEnvironment environment, AuditService audit)
    {
        _context = context;
        _audit = audit;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> GetDevices()
    {
        var devices = await _context.Devices
            .AsNoTracking()
            .Include(x => x.AssignedUser)
            .OrderByDescending(x => x.Id)
            .Select(device => new
            {
                device.Id,
                name = device.DeviceName,
                code = device.DeviceCode,
                serial = !string.IsNullOrWhiteSpace(device.SerialNumber) ? device.SerialNumber : device.ImeiSerial,
                imeiSerial = device.ImeiSerial,
                serialNumber = device.SerialNumber,
                type = device.DeviceType,
                platform = device.Platform,
                brand = device.Brand,
                model = device.Model,
                ram = device.Ram,
                storage = device.Storage,
                storage1 = device.Storage1,
                storage2 = device.Storage2,
                cpu = device.Cpu,
                gpu = device.Gpu,
                screens = device.Screens,
                printer = device.Printer,
                scanner = device.Scanner,
                status = device.Status,
                notes = device.Notes,
                internalNotes = device.InternalNotes,
                imageUrl = device.ImageUrl,
                assignedUser = device.AssignedUser == null ? "—" : device.AssignedUser.FullName,
                initials = device.AssignedUser == null ? "" : GetInitials(device.AssignedUser.FullName)
            })
            .ToListAsync();

        return Ok(devices);
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableDevices()
    {
        var devices = await _context.Devices
            .AsNoTracking()
            .Where(x => x.Status == DeviceStatuses.Available)
            .OrderBy(x => x.DeviceName)
            .Select(device => new
            {
                device.Id,
                name = device.DeviceName,
                code = device.DeviceCode,
                serial = !string.IsNullOrWhiteSpace(device.SerialNumber) ? device.SerialNumber : device.ImeiSerial,
                imeiSerial = device.ImeiSerial,
                serialNumber = device.SerialNumber,
                type = device.DeviceType,
                model = device.Model,
                platform = device.Platform,
                brand = device.Brand,
                ram = device.Ram,
                storage = device.Storage,
                storage1 = device.Storage1,
                storage2 = device.Storage2,
                cpu = device.Cpu,
                gpu = device.Gpu,
                screens = device.Screens,
                printer = device.Printer,
                scanner = device.Scanner,
                imageUrl = device.ImageUrl
            })
            .ToListAsync();

        return Ok(devices);
    }

    [HttpGet("by-code/{code}")]
    public async Task<IActionResult> GetDeviceByCode(string code)
    {
        var device = await _context.Devices
            .AsNoTracking()
            .Include(x => x.AssignedUser)
            .FirstOrDefaultAsync(x => x.DeviceCode == code);

        if (device == null)
            return NotFound(new { message = "Device not found" });

        return Ok(new
        {
            device.Id,
            name = device.DeviceName,
            code = device.DeviceCode,
            serial = !string.IsNullOrWhiteSpace(device.SerialNumber) ? device.SerialNumber : device.ImeiSerial,
            imeiSerial = device.ImeiSerial,
            serialNumber = device.SerialNumber,
            type = device.DeviceType,
            platform = device.Platform,
            brand = device.Brand,
            model = device.Model,
            ram = device.Ram,
            storage = device.Storage,
            storage1 = device.Storage1,
            storage2 = device.Storage2,
            cpu = device.Cpu,
            gpu = device.Gpu,
            screens = device.Screens,
            printer = device.Printer,
            scanner = device.Scanner,
            status = device.Status,
            notes = device.Notes,
            internalNotes = device.InternalNotes,
            imageUrl = device.ImageUrl,
            assignedUser = device.AssignedUser == null ? "—" : device.AssignedUser.FullName,
            initials = device.AssignedUser == null ? "" : GetInitials(device.AssignedUser.FullName)
        });
    }

    [Authorize(Policy = Policies.CanEdit)]
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> CreateDevice([FromForm] CreateDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceName))
            return BadRequest(new { message = "Device name is required" });

        var status = string.IsNullOrWhiteSpace(request.Status) ? DeviceStatuses.Available : request.Status.Trim();

        if (!DeviceStatuses.Editable.Contains(status))
            return BadRequest(new { message = $"Status must be one of: {string.Join(", ", DeviceStatuses.Editable)}. Use Assignments to assign a device." });

        var imageUrl = await SaveDeviceImageAsync(request.ImageFile);

        if (string.IsNullOrWhiteSpace(imageUrl))
            imageUrl = request.ImageUrl?.Trim();

        var device = new Device
        {
            DeviceCode = await CodeGenerator.NextAsync(_context.Devices.Select(x => x.DeviceCode), "DEV-", 4),
            DeviceName = request.DeviceName.Trim(),
            ImeiSerial = request.ImeiSerial?.Trim(),
            SerialNumber = request.SerialNumber?.Trim(),
            DeviceType = request.DeviceType?.Trim() ?? "",
            Platform = string.IsNullOrWhiteSpace(request.Platform)
                ? request.DeviceType?.Trim() ?? ""
                : request.Platform.Trim(),
            Brand = request.Brand?.Trim() ?? "",
            Model = request.Model?.Trim() ?? "",
            Ram = request.Ram?.Trim() ?? "",
            Storage = request.Storage?.Trim() ?? "",
            Storage1 = request.Storage1?.Trim(),
            Storage2 = request.Storage2?.Trim(),
            Cpu = request.Cpu?.Trim(),
            Gpu = request.Gpu?.Trim(),
            Screens = request.Screens,
            Printer = request.Printer,
            Scanner = request.Scanner,
            Status = status,
            Notes = request.Notes?.Trim() ?? "",
            InternalNotes = request.InternalNotes?.Trim(),
            ImageUrl = imageUrl,
            CreatedAt = DateTime.UtcNow
        };

        _context.Devices.Add(device);

        _audit.Log(AuditActions.Create, $"{device.DeviceName} ({device.DeviceCode})", $"Added device {device.DeviceName}");

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Device created successfully",
            device.DeviceCode,
            device.ImageUrl
        });
    }

    [Authorize(Policy = Policies.CanEdit)]
    [HttpPut("by-code/{code}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> UpdateDeviceByCode(string code, [FromForm] CreateDeviceRequest request)
    {
        var device = await _context.Devices
            .Include(x => x.AssignedUser)
            .FirstOrDefaultAsync(x => x.DeviceCode == code);

        if (device == null)
            return NotFound(new { message = "Device not found" });

        if (string.IsNullOrWhiteSpace(request.DeviceName))
            return BadRequest(new { message = "Device name is required" });

        var status = string.IsNullOrWhiteSpace(request.Status) ? device.Status : request.Status.Trim();
        var statusError = await ApplyStatusChangeAsync(device, status);
        if (statusError != null)
            return BadRequest(new { message = statusError });

        var imageUrl = await SaveDeviceImageAsync(request.ImageFile);

        device.DeviceName = request.DeviceName.Trim();
        device.ImeiSerial = request.ImeiSerial?.Trim();
        device.SerialNumber = request.SerialNumber?.Trim();
        device.DeviceType = request.DeviceType?.Trim() ?? "";
        device.Platform = string.IsNullOrWhiteSpace(request.Platform)
            ? request.DeviceType?.Trim() ?? ""
            : request.Platform.Trim();
        device.Brand = request.Brand?.Trim() ?? "";
        device.Model = request.Model?.Trim() ?? "";
        device.Ram = request.Ram?.Trim() ?? "";
        device.Storage = request.Storage?.Trim() ?? "";
        device.Storage1 = request.Storage1?.Trim();
        device.Storage2 = request.Storage2?.Trim();
        device.Cpu = request.Cpu?.Trim();
        device.Gpu = request.Gpu?.Trim();
        device.Screens = request.Screens;
        device.Printer = request.Printer;
        device.Scanner = request.Scanner;
        device.Notes = request.Notes?.Trim() ?? "";
        device.InternalNotes = request.InternalNotes?.Trim();

        if (!string.IsNullOrWhiteSpace(imageUrl))
            device.ImageUrl = imageUrl;
        else if (!string.IsNullOrWhiteSpace(request.ImageUrl))
            device.ImageUrl = request.ImageUrl.Trim();

        _audit.Log(AuditActions.Update, $"{device.DeviceName} ({device.DeviceCode})", $"Updated device {device.DeviceName}");

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Device updated successfully",
            device.ImageUrl
        });
    }

    [Authorize(Policy = Policies.CanEdit)]
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] string status)
    {
        var device = await _context.Devices
            .Include(x => x.AssignedUser)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (device == null)
            return NotFound(new { message = "Device not found" });

        var statusError = await ApplyStatusChangeAsync(device, status?.Trim() ?? "");
        if (statusError != null)
            return BadRequest(new { message = statusError });

        _audit.Log(
            device.Status == DeviceStatuses.Lost ? AuditActions.Alert : AuditActions.Update,
            $"{device.DeviceName} ({device.DeviceCode})",
            $"Changed device status to {device.Status}");

        await _context.SaveChangesAsync();

        return Ok(new { message = "Device status updated successfully" });
    }

    [Authorize(Policy = Policies.CanEdit)]
    [HttpDelete("by-code/{code}")]
    public async Task<IActionResult> DeleteDeviceByCode(string code)
    {
        var device = await _context.Devices.FirstOrDefaultAsync(x => x.DeviceCode == code);

        if (device == null)
            return NotFound(new { message = "Device not found" });

        if (await _context.Assignments.AnyAsync(x => x.DeviceId == device.Id))
            return Conflict(new { message = "This device has assignment history and cannot be deleted. Set its status to Inactive instead." });

        _context.Devices.Remove(device);

        _audit.Log(AuditActions.Delete, $"{device.DeviceName} ({device.DeviceCode})", $"Deleted device {device.DeviceName}");

        await _context.SaveChangesAsync();

        return Ok(new { message = "Device deleted successfully" });
    }

    /// <summary>
    /// Applies a status change without saving. Assigned can only be entered through
    /// Assignments; leaving Assigned closes the active assignment so both stay in sync.
    /// Returns an error message, or null on success.
    /// </summary>
    private async Task<string?> ApplyStatusChangeAsync(Device device, string newStatus)
    {
        if (newStatus == device.Status)
            return null;

        if (!DeviceStatuses.All.Contains(newStatus))
            return $"Status must be one of: {string.Join(", ", DeviceStatuses.Editable)}";

        if (newStatus == DeviceStatuses.Assigned)
            return "Use Assignments to assign a device to an employee.";

        if (device.Status == DeviceStatuses.Assigned)
        {
            var activeAssignment = await _context.Assignments
                .FirstOrDefaultAsync(x => x.DeviceId == device.Id && x.Status == AssignmentStatuses.Active);

            if (activeAssignment != null)
            {
                activeAssignment.Status = AssignmentStatuses.Returned;
                activeAssignment.ReturnedDate = DateTime.UtcNow;

                _audit.Log(
                    AuditActions.Return,
                    $"{device.AssignedUser?.FullName} ({device.AssignedUser?.EmployeeId})",
                    $"Returned {device.DeviceName} ({device.DeviceCode}) by changing its status to {newStatus}");
            }
        }

        device.Status = newStatus;
        device.AssignedUserId = null;

        return null;
    }

    private async Task<string?> SaveDeviceImageAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return null;

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".jfif" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            throw new ApiException(StatusCodes.Status400BadRequest, "Invalid image type");

        var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var folder = Path.Combine(webRoot, "uploads", "devices");

        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(folder, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host}/uploads/devices/{fileName}";
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