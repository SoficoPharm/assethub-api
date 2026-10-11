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
                role = user.Role,
                devices = _context.Devices.Count(d => d.AssignedUserId == user.Id),
                images = user.ImagesCount,
                imageUrl = user.ImageUrl,
                userDomain = user.UserDomain,
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
            return NotFound(new { message = "User not found" });

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
            role = user.Role,
            images = user.ImagesCount,
            imageUrl = user.ImageUrl,
            userDomain = user.UserDomain,
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
            return NotFound(new { message = "User not found" });

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
            role = user.Role,
            images = user.ImagesCount,
            imageUrl = user.ImageUrl,
            userDomain = user.UserDomain,
            initials = GetInitials(user.FullName)
        });
    }

    [Authorize(Policy = Policies.CanEdit)]
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> CreateUser([FromForm] CreateUserRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
                return BadRequest(new { message = "Full name is required" });

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Email is required" });

            var emailExists = await _context.Users.AnyAsync(x => x.Email == request.Email.Trim());
            if (emailExists)
                return BadRequest(new { message = "Email already exists" });

            var employeeId = string.IsNullOrWhiteSpace(request.EmployeeId)
                ? await GenerateEmployeeId()
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
                UserName = request.Email.Trim().Split('@')[0],
                Email = request.Email.Trim(),
                EmployeeId = employeeId,
                Department = request.Department?.Trim() ?? "",
                JobTitle = request.JobTitle?.Trim() ?? "",
                UserDomain = request.UserDomain?.Trim() ?? "",
                Role = string.IsNullOrWhiteSpace(request.Role) ? "Viewer" : request.Role.Trim(),
                ImagesCount = imageUrl == null ? request.Images : 1,
                ImageUrl = imageUrl,
                IsActive = true,

                CreatedAt = DateTime.Now
            };

            _context.Users.Add(user);

            _audit.Log(AuditActions.Create, $"{user.FullName} ({user.EmployeeId})", "Created employee");

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "User created successfully",
                user.Id,
                user.EmployeeId,
                user.ImageUrl
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Policy = Policies.CanEdit)]
    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> UpdateUser(int id, [FromForm] CreateUserRequest request)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);

            if (user == null)
                return NotFound(new { message = "User not found" });

            if (string.IsNullOrWhiteSpace(request.FullName))
                return BadRequest(new { message = "Full name is required" });

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Email is required" });

            var emailExists = await _context.Users.AnyAsync(x => x.Email == request.Email.Trim() && x.Id != id);
            if (emailExists)
                return BadRequest(new { message = "Email already exists" });

            var imageUrl = await SaveUserImageAsync(request.ImageFile);

            user.FullName = request.FullName.Trim();
            user.Email = request.Email.Trim();
            user.UserName = request.Email.Trim().Split('@')[0];
            user.EmployeeId = string.IsNullOrWhiteSpace(request.EmployeeId)
                ? user.EmployeeId
                : request.EmployeeId.Trim();
            user.Department = request.Department?.Trim() ?? "";
            user.JobTitle = request.JobTitle?.Trim() ?? "";
            user.UserDomain = request.UserDomain?.Trim() ?? "";
            user.Role = string.IsNullOrWhiteSpace(request.Role) ? "Viewer" : request.Role.Trim();

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
                message = "User updated successfully",
                user.Id,
                user.EmployeeId,
                user.ImageUrl
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    [Authorize(Policy = Policies.CanEdit)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
            return NotFound(new { message = "User not found" });

        var assignments = await _context.Assignments
            .Where(x => x.UserId == id)
            .ToListAsync();

        _context.Assignments.RemoveRange(assignments);

        var assignedDevices = await _context.Devices
            .Where(x => x.AssignedUserId == id)
            .ToListAsync();

        foreach (var device in assignedDevices)
        {
            device.AssignedUserId = null;
            device.Status = "Available";
        }

        _context.Users.Remove(user);

        _audit.Log(AuditActions.Delete, $"{user.FullName} ({user.EmployeeId})", "Deleted employee");

        await _context.SaveChangesAsync();

        return Ok(new { message = "User deleted successfully" });
    }

    private async Task<string?> SaveUserImageAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return null;

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".jfif" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            throw new InvalidOperationException("Invalid image type");

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

    private async Task<string> GenerateEmployeeId()
    {
        var count = await _context.Users.CountAsync();
        return $"EMP-{count + 1:000}";
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