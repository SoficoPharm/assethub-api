using System.Linq.Expressions;
using AssetHub.Api.Authorization;
using AssetHub.Api.Data;
using AssetHub.Api.DTOs;
using AssetHub.Api.Models;
using AssetHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Api.Controllers;

/// <summary>
/// Login accounts. SuperAdmin only. Passwords are never returned.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.RequireSuperAdmin)]
public class SystemUsersController : ControllerBase
{
    private static readonly Expression<Func<SystemUser, SystemUserDto>> ToDtoExpression = x => new SystemUserDto
    {
        Id = x.Id,
        FullName = x.FullName,
        UserName = x.UserName,
        Email = x.Email,
        Department = x.Department,
        Role = x.Role,
        IsActive = x.IsActive,
        CreatedAt = x.CreatedAt
    };

    private static readonly Func<SystemUser, SystemUserDto> ToDto = ToDtoExpression.Compile();

    private readonly AppDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly AuditService _audit;

    public SystemUsersController(AppDbContext context, ICurrentUser currentUser, AuditService audit)
    {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _context.SystemUsers
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(ToDtoExpression)
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _context.SystemUsers
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(ToDtoExpression)
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound(new { message = "System user not found" });

        return Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSystemUserRequest request)
    {
        var error = ValidateProfile(request.FullName, request.UserName, request.Role)
                    ?? ValidatePassword(request.Password);
        if (error != null)
            return BadRequest(new { message = error });

        var userName = request.UserName.Trim();

        if (await _context.SystemUsers.AnyAsync(x => x.UserName == userName))
            return Conflict(new { message = "User name already exists" });

        var user = new SystemUser
        {
            FullName = request.FullName.Trim(),
            UserName = userName,
            Email = request.Email?.Trim() ?? "",
            Department = request.Department?.Trim() ?? "",
            Role = request.Role,
            Password = PasswordHasher.Hash(request.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.SystemUsers.Add(user);
        _audit.Log(AuditActions.Create, userName, $"Created system user {userName} with role {user.Role}");

        await _context.SaveChangesAsync();

        return Ok(ToDto(user));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSystemUserRequest request)
    {
        var user = await _context.SystemUsers.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "System user not found" });

        var error = ValidateProfile(request.FullName, request.UserName, request.Role);
        if (error != null)
            return BadRequest(new { message = error });

        var userName = request.UserName.Trim();

        if (await _context.SystemUsers.AnyAsync(x => x.Id != id && x.UserName == userName))
            return Conflict(new { message = "User name already exists" });

        var isDemotion = user.Role == Roles.SuperAdmin && request.Role != Roles.SuperAdmin;

        if (isDemotion && IsSelf(user))
            return BadRequest(new { message = "You cannot remove your own SuperAdmin role" });

        if (isDemotion && user.IsActive && await IsLastActiveSuperAdmin(user.Id))
            return Conflict(new { message = "At least one active SuperAdmin must remain" });

        var oldRole = user.Role;

        user.FullName = request.FullName.Trim();
        user.UserName = userName;
        user.Email = request.Email?.Trim() ?? "";
        user.Department = request.Department?.Trim() ?? "";
        user.Role = request.Role;

        var details = oldRole == user.Role
            ? $"Updated system user {userName}"
            : $"Updated system user {userName}, role {oldRole} -> {user.Role}";
        _audit.Log(AuditActions.Update, userName, details);

        await _context.SaveChangesAsync();

        return Ok(ToDto(user));
    }

    [HttpPut("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
    {
        var user = await _context.SystemUsers.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "System user not found" });

        var error = ValidatePassword(request.NewPassword);
        if (error != null)
            return BadRequest(new { message = error });

        user.Password = PasswordHasher.Hash(request.NewPassword);
        _audit.Log(AuditActions.Update, user.UserName, $"Reset password for {user.UserName}");

        await _context.SaveChangesAsync();

        return Ok(new { message = "Password reset successfully" });
    }

    [HttpPut("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await _context.SystemUsers.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "System user not found" });

        if (user.IsActive)
        {
            if (IsSelf(user))
                return BadRequest(new { message = "You cannot deactivate your own account" });

            if (user.Role == Roles.SuperAdmin && await IsLastActiveSuperAdmin(user.Id))
                return Conflict(new { message = "At least one active SuperAdmin must remain" });
        }

        user.IsActive = !user.IsActive;
        _audit.Log(
            AuditActions.Update,
            user.UserName,
            $"{(user.IsActive ? "Activated" : "Deactivated")} system user {user.UserName}");

        await _context.SaveChangesAsync();

        return Ok(ToDto(user));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _context.SystemUsers.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "System user not found" });

        if (IsSelf(user))
            return BadRequest(new { message = "You cannot delete your own account" });

        if (user.Role == Roles.SuperAdmin && user.IsActive && await IsLastActiveSuperAdmin(user.Id))
            return Conflict(new { message = "At least one active SuperAdmin must remain" });

        _context.SystemUsers.Remove(user);
        _audit.Log(AuditActions.Delete, user.UserName, $"Deleted system user {user.UserName}");

        await _context.SaveChangesAsync();

        return Ok(new { message = "System user deleted successfully" });
    }

    private bool IsSelf(SystemUser user) => _currentUser.Id == user.Id;

    /// <summary>True if no active SuperAdmin exists other than <paramref name="userId"/>.</summary>
    private async Task<bool> IsLastActiveSuperAdmin(int userId) =>
        !await _context.SystemUsers.AnyAsync(x => x.Id != userId && x.Role == Roles.SuperAdmin && x.IsActive);

    private static string? ValidateProfile(string? fullName, string? userName, string? role)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "Full name is required";

        if (string.IsNullOrWhiteSpace(userName))
            return "User name is required";

        if (!Roles.IsValid(role))
            return $"Role must be one of: {string.Join(", ", Roles.All)}";

        return null;
    }

    private static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < PasswordHasher.MinLength)
            return $"Password must be at least {PasswordHasher.MinLength} characters";

        return null;
    }
}
