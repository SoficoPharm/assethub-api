using System.Globalization;
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

                screenSizeInches = device.ScreenSizeInches,
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

                screenSizeInches = device.ScreenSizeInches,
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

            screenSizeInches = device.ScreenSizeInches,
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

    /// <summary>Lets the add/edit forms warn about a duplicate serial before saving.</summary>
    [Authorize(Policy = Policies.CanCreateOrEdit)]
    [HttpGet("check-serial")]
    public async Task<IActionResult> CheckSerial([FromQuery] string? serial, [FromQuery] string? excludeCode)
    {
        var normalized = SerialNumbers.Normalize(serial);

        int? excludeId = null;
        if (!string.IsNullOrWhiteSpace(excludeCode))
        {
            excludeId = await _context.Devices
                .Where(x => x.DeviceCode == excludeCode)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();
        }

        var owner = await FindSerialOwnerAsync(normalized, excludeId);

        return Ok(new
        {
            exists = owner != null,
            deviceCode = owner?.DeviceCode,
            deviceName = owner?.DeviceName
        });
    }

    [Authorize(Policy = Policies.CanCreateOrEdit)]
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> CreateDevice([FromForm] CreateDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceName))
            return BadRequest(new { message = "Device name is required" });

        var fieldError = ValidateDeviceFields(request, out var screenSizeInches);
        if (fieldError != null)
            return BadRequest(new { message = fieldError });

        var status = string.IsNullOrWhiteSpace(request.Status) ? DeviceStatuses.Available : request.Status.Trim();

        if (!DeviceStatuses.Editable.Contains(status))
            return BadRequest(new { message = $"Status must be one of: {string.Join(", ", DeviceStatuses.Editable)}. Use Assignments to assign a device." });

        var serial = SerialNumbers.Normalize(request.SerialNumber);
        var serialOwner = await FindSerialOwnerAsync(serial, excludeDeviceId: null);
        if (serialOwner != null)
            return SerialConflict(serial!, serialOwner);

        var imageUrl = await SaveDeviceImageAsync(request.ImageFile);

        if (string.IsNullOrWhiteSpace(imageUrl))
            imageUrl = request.ImageUrl?.Trim();

        var device = new Device
        {
            DeviceCode = await CodeGenerator.NextAsync(_context.Devices.Select(x => x.DeviceCode), "DEV-", 4),
            DeviceName = request.DeviceName.Trim(),
            ImeiSerial = request.ImeiSerial?.Trim(),
            SerialNumber = serial,
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
            ScreenSizeInches = screenSizeInches,
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

        var raceConflict = await SaveCheckingSerialAsync(serial, excludeDeviceId: null);
        if (raceConflict != null)
            return raceConflict;

        return Ok(new
        {
            message = "Device created successfully",
            device.DeviceCode,
            device.ImageUrl
        });
    }

    [Authorize(Policy = Policies.CanCreateOrEdit)]
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

        var fieldError = ValidateDeviceFields(request, out var screenSizeInches);
        if (fieldError != null)
            return BadRequest(new { message = fieldError });

        var serial = SerialNumbers.Normalize(request.SerialNumber);
        var serialOwner = await FindSerialOwnerAsync(serial, excludeDeviceId: device.Id);
        if (serialOwner != null)
            return SerialConflict(serial!, serialOwner);

        var status = string.IsNullOrWhiteSpace(request.Status) ? device.Status : request.Status.Trim();
        var statusError = await ApplyStatusChangeAsync(device, status);
        if (statusError != null)
            return BadRequest(new { message = statusError });

        var imageUrl = await SaveDeviceImageAsync(request.ImageFile);

        device.DeviceName = request.DeviceName.Trim();
        device.ImeiSerial = request.ImeiSerial?.Trim();
        device.SerialNumber = serial;
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
        device.ScreenSizeInches = screenSizeInches;
        device.Printer = request.Printer;
        device.Scanner = request.Scanner;
        device.Notes = request.Notes?.Trim() ?? "";
        device.InternalNotes = request.InternalNotes?.Trim();

        if (!string.IsNullOrWhiteSpace(imageUrl))
            device.ImageUrl = imageUrl;
        else if (!string.IsNullOrWhiteSpace(request.ImageUrl))
            device.ImageUrl = request.ImageUrl.Trim();

        _audit.Log(AuditActions.Update, $"{device.DeviceName} ({device.DeviceCode})", $"Updated device {device.DeviceName}");

        var raceConflict = await SaveCheckingSerialAsync(serial, excludeDeviceId: device.Id);
        if (raceConflict != null)
            return raceConflict;

        return Ok(new
        {
            message = "Device updated successfully",
            device.ImageUrl
        });
    }

    [Authorize(Policy = Policies.CanCreateOrEdit)]
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

    [Authorize(Policy = Policies.CanDelete)]
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

    /// <summary>Device types (case-insensitive) that must have RAM, Storage and CPU.</summary>
    private static readonly string[] TypesRequiringHardware = { "Laptop", "Desktop" };

    /// <summary>Device types (case-insensitive) that may be saved without a serial number.</summary>
    private static readonly string[] TypesWithoutSerial = { "Bag", "SIM Line" };

    public const decimal MinScreenSizeInches = 7m;
    public const decimal MaxScreenSizeInches = 100m;

    /// <summary>
    /// Field rules shared by create and update. Returns an error message, or null when valid.
    /// - Device Type is required.
    /// - Serial Number is required except for Bag and SIM Line.
    /// - Laptop and Desktop need RAM, Storage and CPU; "N/A" does not count as filled for them.
    /// - Screen size, when given, is 7–100 inches with at most one decimal.
    /// </summary>
    private static string? ValidateDeviceFields(CreateDeviceRequest request, out decimal? screenSizeInches)
    {
        screenSizeInches = null;

        var type = request.DeviceType?.Trim() ?? "";
        if (type.Length == 0)
            return "Device type is required";

        if (!TypesWithoutSerial.Contains(type, StringComparer.OrdinalIgnoreCase)
            && SerialNumbers.Normalize(request.SerialNumber) == null)
            return $"Serial number is required for {type} devices";

        if (TypesRequiringHardware.Contains(type, StringComparer.OrdinalIgnoreCase))
        {
            static bool IsMissing(string? value) =>
                string.IsNullOrWhiteSpace(value) || value.Trim().Equals("N/A", StringComparison.OrdinalIgnoreCase);

            var missing = new List<string>();
            if (IsMissing(request.Ram)) missing.Add("RAM");
            if (IsMissing(request.Storage)) missing.Add("Storage");
            if (IsMissing(request.Cpu)) missing.Add("CPU");

            if (missing.Count > 0)
                return $"RAM, Storage and CPU are required for {type} devices (N/A is not accepted). Missing: {string.Join(", ", missing)}.";
        }

        return ParseScreenSize(request.ScreenSizeInches, out screenSizeInches);
    }

    /// <summary>
    /// Parses the optional screen size. The form sends it as text and it is always read with
    /// a "." decimal separator, whatever the server's culture. Returns an error message or null.
    /// </summary>
    private static string? ParseScreenSize(string? text, out decimal? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (!decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var size))
            return "Screen size must be a number, e.g. 15.6";

        if (size < MinScreenSizeInches || size > MaxScreenSizeInches)
            return $"Screen size must be between {MinScreenSizeInches} and {MaxScreenSizeInches} inches";

        if (decimal.Round(size, 1) != size)
            return "Screen size can have at most one decimal, e.g. 15.6";

        value = size;
        return null;
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

    /// <summary>The device that already uses <paramref name="serial"/> (normalized), other than the excluded one.</summary>
    private Task<Device?> FindSerialOwnerAsync(string? serial, int? excludeDeviceId)
    {
        if (serial == null)
            return Task.FromResult<Device?>(null);

        return _context.Devices
            .AsNoTracking()
            .Where(x => x.SerialNumber == serial && (excludeDeviceId == null || x.Id != excludeDeviceId))
            .FirstOrDefaultAsync();
    }

    private ConflictObjectResult SerialConflict(string serial, Device owner) =>
        Conflict(new { message = SerialNumbers.ConflictMessage(serial, owner.DeviceCode, owner.DeviceName) });

    /// <summary>
    /// Saves, turning a unique-index violation on the serial (two requests racing past the
    /// pre-check) into the same 409 as the pre-check. Returns null on success.
    /// </summary>
    private async Task<IActionResult?> SaveCheckingSerialAsync(string? serial, int? excludeDeviceId)
    {
        try
        {
            await _context.SaveChangesAsync();
            return null;
        }
        catch (DbUpdateException ex) when (serial != null && SerialNumbers.IsUniqueViolation(ex))
        {
            var owner = await FindSerialOwnerAsync(serial, excludeDeviceId);
            if (owner == null)
                throw;

            return SerialConflict(serial, owner);
        }
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