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
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AuditService _audit;
    private readonly IWebHostEnvironment _environment;

    public UsersController(AppDbContext context, IWebHostEnvironment environment, AuditService audit)
    {
        _context = context;
        _audit = audit;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(user => new
            {
                user.Id,
                name = user.FullName,
                email = user.Email,
                employeeId = user.EmployeeId,
                department = user.Department,
                title = user.JobTitle,
                devices = _context.Devices.Count(d => d.AssignedUserId == user.Id),
                images = user.ImagesCount,
                imageUrl = user.ImageUrl,
                userDomain = user.UserDomain,
                isActive = user.IsActive,
                initials = GetInitials(user.FullName)
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUserById(int id)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
            return NotFound(new { message = "Employee not found" });

        return Ok(new
        {
            user.Id,
            name = user.FullName,
            fullName = user.FullName,
            email = user.Email,
            employeeId = user.EmployeeId,
            department = user.Department,
            jobTitle = user.JobTitle,
            title = user.JobTitle,
            images = user.ImagesCount,
            imageUrl = user.ImageUrl,
            userDomain = user.UserDomain,
            isActive = user.IsActive,
            initials = GetInitials(user.FullName)
        });
    }

    [HttpGet("by-employee/{employeeId}")]
    public async Task<IActionResult> GetUserByEmployeeId(string employeeId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId);

        if (user == null)
            return NotFound(new { message = "Employee not found" });

        return Ok(new
        {
            user.Id,
            name = user.FullName,
            fullName = user.FullName,
            email = user.Email,
            employeeId = user.EmployeeId,
            department = user.Department,
            jobTitle = user.JobTitle,
            title = user.JobTitle,
            images = user.ImagesCount,
            imageUrl = user.ImageUrl,
            userDomain = user.UserDomain,
            isActive = user.IsActive,
            initials = GetInitials(user.FullName)
        });
    }

    [Authorize(Policy = Policies.CanCreateOrEdit)]
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> CreateUser([FromForm] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "Full name is required" });

        var email = NormalizeEmail(request.Email);

        if (email != null && await _context.Users.AnyAsync(x => x.Email == email))
            return BadRequest(new { message = "Email already exists" });

        var employeeId = string.IsNullOrWhiteSpace(request.EmployeeId)
            ? await CodeGenerator.NextAsync(_context.Users.Select(x => x.EmployeeId), "EMP-", 3)
            : request.EmployeeId.Trim();

        var employeeExists = await _context.Users.AnyAsync(x => x.EmployeeId == employeeId);
        if (employeeExists)
            return BadRequest(new { message = "Employee ID already exists" });

        var imageUrl = await SaveUserImageAsync(request.ImageFile);

        if (string.IsNullOrWhiteSpace(imageUrl))
            imageUrl = request.ImageUrl?.Trim();

        var user = new AppUser
        {
            FullName = request.FullName.Trim(),
            UserName = DeriveUserName(email, employeeId),
            Email = email,
            EmployeeId = employeeId,
            Department = request.Department?.Trim() ?? "",
            JobTitle = request.JobTitle?.Trim() ?? "",
            UserDomain = request.UserDomain?.Trim() ?? "",
            ImagesCount = imageUrl == null ? request.Images : 1,
            ImageUrl = imageUrl,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);

        _audit.Log(AuditActions.Create, $"{user.FullName} ({user.EmployeeId})", "Created employee");

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Employee created successfully",
            user.Id,
            user.EmployeeId,
            user.ImageUrl
        });
    }

    [Authorize(Policy = Policies.CanCreateOrEdit)]
    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> UpdateUser(int id, [FromForm] CreateUserRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
            return NotFound(new { message = "Employee not found" });

        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "Full name is required" });

        var email = NormalizeEmail(request.Email);

        if (email != null && await _context.Users.AnyAsync(x => x.Email == email && x.Id != id))
            return BadRequest(new { message = "Email already exists" });

        var employeeId = string.IsNullOrWhiteSpace(request.EmployeeId) ? user.EmployeeId : request.EmployeeId.Trim();

        var employeeExists = await _context.Users.AnyAsync(x => x.EmployeeId == employeeId && x.Id != id);
        if (employeeExists)
            return BadRequest(new { message = "Employee ID already exists" });

        var imageUrl = await SaveUserImageAsync(request.ImageFile);

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.UserName = DeriveUserName(email, employeeId);
        user.EmployeeId = employeeId;
        user.Department = request.Department?.Trim() ?? "";
        user.JobTitle = request.JobTitle?.Trim() ?? "";
        user.UserDomain = request.UserDomain?.Trim() ?? "";

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            user.ImageUrl = imageUrl;
            user.ImagesCount = 1;
        }
        else if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            user.ImageUrl = request.ImageUrl.Trim();
            user.ImagesCount = 1;
        }
        else
        {
            user.ImageUrl = null;
            user.ImagesCount = 0;
        }

        _audit.Log(AuditActions.Update, $"{user.FullName} ({user.EmployeeId})", "Updated employee");

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Employee updated successfully",
            user.Id,
            user.EmployeeId,
            user.ImageUrl
        });
    }

    [Authorize(Policy = Policies.CanCreateOrEdit)]
    [HttpPut("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
            return NotFound(new { message = "Employee not found" });

        if (user.IsActive)
        {
            var holdsDevices = await _context.Assignments.AnyAsync(x => x.UserId == id && x.Status == AssignmentStatuses.Active)
                               || await _context.Devices.AnyAsync(x => x.AssignedUserId == id);

            if (holdsDevices)
                return Conflict(new { message = "This employee still has assigned devices. Return their devices first." });
        }

        user.IsActive = !user.IsActive;

        _audit.Log(
            AuditActions.Update,
            $"{user.FullName} ({user.EmployeeId})",
            user.IsActive ? "Activated employee" : "Deactivated employee");

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = user.IsActive ? "Employee activated" : "Employee deactivated",
            user.Id,
            isActive = user.IsActive
        });
    }

    [Authorize(Policy = Policies.CanDelete)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
            return NotFound(new { message = "Employee not found" });

        // Assignment history is never deleted.
        var hasHistory = await _context.Assignments.AnyAsync(x => x.UserId == id)
                         || await _context.Devices.AnyAsync(x => x.AssignedUserId == id);

        if (hasHistory)
            return Conflict(new { message = "This employee has assignment history and cannot be deleted. Deactivate them instead." });

        _context.Users.Remove(user);

        _audit.Log(AuditActions.Delete, $"{user.FullName} ({user.EmployeeId})", "Deleted employee");

        await _context.SaveChangesAsync();

        return Ok(new { message = "Employee deleted successfully" });
    }

    private async Task<string?> SaveUserImageAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return null;

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".jfif" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            throw new ApiException(StatusCodes.Status400BadRequest, "Invalid image type");

        var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var folder = Path.Combine(webRoot, "uploads", "users");

        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(folder, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host}/uploads/users/{fileName}";
    }

    /// <summary>Email is optional: empty or whitespace is stored as NULL.</summary>
    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim();

    /// <summary>The email prefix, or the employee ID when there is no email.</summary>
    private static string DeriveUserName(string? email, string employeeId) =>
        email != null ? email.Split('@')[0] : employeeId;

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