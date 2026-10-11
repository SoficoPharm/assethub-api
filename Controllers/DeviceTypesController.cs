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
public class DeviceTypesController : ControllerBase
{
    private readonly AppDbContext _context;

    public DeviceTypesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var data = await _context.DeviceTypes
            .AsNoTracking()
            .OrderBy(x => x.TypeName)
            .Select(x => new DeviceTypeDto
            {
                Id = x.Id,
                TypeName = x.TypeName,
                Icon = x.Icon,
                DevicesCount = x.DevicesCount
            })
            .ToListAsync();

        return Ok(data);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDeviceTypeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.TypeName))
            return BadRequest(new { message = "Type name is required" });

        var typeName = dto.TypeName.Trim();

        var exists = await _context.DeviceTypes
            .AnyAsync(x => x.TypeName.ToLower() == typeName.ToLower());

        if (exists)
            return BadRequest(new { message = "Device type already exists" });

        var item = new DeviceType
        {
            TypeName = typeName,
            Icon = string.IsNullOrWhiteSpace(dto.Icon) ? "📦" : dto.Icon.Trim(),
            DevicesCount = dto.DevicesCount < 0 ? 0 : dto.DevicesCount
        };

        _context.DeviceTypes.Add(item);

        _context.AuditLogs.Add(new AuditLog
        {
            Actor = "Admin User",
            LogAction = "Create",
            Target = typeName,
            Details = $"Created device type {typeName}",
            Timestamp = DateTime.Now
        });

        await _context.SaveChangesAsync();

        return Ok(new DeviceTypeDto
        {
            Id = item.Id,
            TypeName = item.TypeName,
            Icon = item.Icon,
            DevicesCount = item.DevicesCount
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDeviceTypeDto dto)
    {
        var item = await _context.DeviceTypes.FindAsync(id);

        if (item == null)
            return NotFound(new { message = "Device type not found" });

        if (string.IsNullOrWhiteSpace(dto.TypeName))
            return BadRequest(new { message = "Type name is required" });

        var oldName = item.TypeName;
        var newName = dto.TypeName.Trim();

        var exists = await _context.DeviceTypes
            .AnyAsync(x => x.Id != id && x.TypeName.ToLower() == newName.ToLower());

        if (exists)
            return BadRequest(new { message = "Device type already exists" });

        item.TypeName = newName;
        item.Icon = string.IsNullOrWhiteSpace(dto.Icon) ? "📦" : dto.Icon.Trim();
        item.DevicesCount = dto.DevicesCount < 0 ? 0 : dto.DevicesCount;

        var devices = await _context.Devices
            .Where(d => d.DeviceType == oldName)
            .ToListAsync();

        foreach (var device in devices)
        {
            device.DeviceType = newName;
            device.Platform = newName;
        }

        _context.AuditLogs.Add(new AuditLog
        {
            Actor = "Admin User",
            LogAction = "Update",
            Target = newName,
            Details = $"Updated device type {oldName} to {newName}",
            Timestamp = DateTime.Now
        });

        await _context.SaveChangesAsync();

        return Ok(new { message = "Device type updated successfully" });
    }



    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.DeviceTypes.FindAsync(id);

        if (item == null)
            return NotFound(new { message = "Device type not found" });

        var hasDevices = await _context.Devices.AnyAsync(d => d.DeviceType == item.TypeName);

        if (hasDevices)
            return BadRequest(new { message = "Cannot delete device type because it has devices" });

        _context.DeviceTypes.Remove(item);

        _context.AuditLogs.Add(new AuditLog
        {
            Actor = "Admin User",
            LogAction = "Delete",
            Target = item.TypeName,
            Details = $"Deleted device type {item.TypeName}",
            Timestamp = DateTime.Now
        });

        await _context.SaveChangesAsync();

        return Ok(new { message = "Device type deleted successfully" });
    }
}