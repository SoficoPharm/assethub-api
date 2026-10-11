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
public class DeviceTypesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AuditService _audit;

    public DeviceTypesController(AppDbContext context, AuditService audit)
    {
        _context = context;
        _audit = audit;
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

    [Authorize(Policy = Policies.CanEdit)]
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

        _audit.Log(AuditActions.Create, typeName, $"Created device type {typeName}");

        await _context.SaveChangesAsync();

        return Ok(new DeviceTypeDto
        {
            Id = item.Id,
            TypeName = item.TypeName,
            Icon = item.Icon,
            DevicesCount = item.DevicesCount
        });
    }

    [Authorize(Policy = Policies.CanEdit)]
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
        }

        _audit.Log(AuditActions.Update, newName, $"Updated device type {oldName} to {newName}");

        await _context.SaveChangesAsync();

        return Ok(new { message = "Device type updated successfully" });
    }



    [Authorize(Policy = Policies.CanEdit)]
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

        _audit.Log(AuditActions.Delete, item.TypeName, $"Deleted device type {item.TypeName}");

        await _context.SaveChangesAsync();

        return Ok(new { message = "Device type deleted successfully" });
    }
}