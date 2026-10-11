using AssetHub.Api.Data;
using AssetHub.Api.Authorization;
using AssetHub.Api.Models;
using AssetHub.Api.DTOs;
using AssetHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssetHub.DTOs;
using AssetHub.Models;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AuditService _audit;

    public DepartmentsController(AppDbContext context, AuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetDepartments()
    {
        var departments = await _context.Departments
            .AsNoTracking()
            .OrderBy(x => x.DepartmentName)
            .Select(x => new DepartmentDto
            {
                Id = x.Id,
                DepartmentName = x.DepartmentName,
                ManagerName = x.ManagerName,
                MembersCount = _context.Users.Count(u => u.Department == x.DepartmentName)
            })
            .ToListAsync();

        return Ok(departments);
    }

    [Authorize(Policy = Policies.CanEdit)]
    [HttpPost]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.DepartmentName))
            return BadRequest(new { message = "Department name is required" });

        var name = dto.DepartmentName.Trim();

        var exists = await _context.Departments
            .AnyAsync(x => x.DepartmentName.ToLower() == name.ToLower());

        if (exists)
            return BadRequest(new { message = "Department already exists" });

        var department = new Department
        {
            DepartmentName = name,
            ManagerName = dto.ManagerName?.Trim() ?? "",
            MembersCount = 0
        };

        _context.Departments.Add(department);

        _audit.Log(AuditActions.Create, name, $"Created department {name}");

        await _context.SaveChangesAsync();

        return Ok(new DepartmentDto
        {
            Id = department.Id,
            DepartmentName = department.DepartmentName,
            ManagerName = department.ManagerName,
            MembersCount = 0
        });
    }

    [Authorize(Policy = Policies.CanEdit)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateDepartment(int id, [FromBody] UpdateDepartmentDto dto)
    {
        var department = await _context.Departments.FindAsync(id);

        if (department == null)
            return NotFound(new { message = "Department not found" });

        if (string.IsNullOrWhiteSpace(dto.DepartmentName))
            return BadRequest(new { message = "Department name is required" });

        var oldName = department.DepartmentName;
        var newName = dto.DepartmentName.Trim();

        var exists = await _context.Departments
            .AnyAsync(x => x.Id != id && x.DepartmentName.ToLower() == newName.ToLower());

        if (exists)
            return BadRequest(new { message = "Department already exists" });

        department.DepartmentName = newName;
        department.ManagerName = dto.ManagerName?.Trim() ?? "";

        var users = await _context.Users
            .Where(u => u.Department == oldName)
            .ToListAsync();

        foreach (var user in users)
            user.Department = newName;

        _audit.Log(AuditActions.Update, newName, $"Updated department {oldName} to {newName}");

        await _context.SaveChangesAsync();

        return Ok(new { message = "Department updated successfully" });
    }

    [Authorize(Policy = Policies.CanEdit)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteDepartment(int id)
    {
        var department = await _context.Departments.FindAsync(id);

        if (department == null)
            return NotFound(new { message = "Department not found" });

        var hasUsers = await _context.Users.AnyAsync(u => u.Department == department.DepartmentName);

        if (hasUsers)
            return BadRequest(new { message = "Cannot delete department because it has users" });

        _context.Departments.Remove(department);

        _audit.Log(AuditActions.Delete, department.DepartmentName, $"Deleted department {department.DepartmentName}");

        await _context.SaveChangesAsync();

        return Ok(new { message = "Department deleted successfully" });
    }
}