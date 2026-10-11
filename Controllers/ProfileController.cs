using AssetHub.Api.Data;
using AssetHub.Api.DTOs;
using AssetHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly AuditService _audit;

    public ProfileController(AppDbContext context, ICurrentUser currentUser, AuditService audit)
    {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var userId = _currentUser.Id;

        if (userId == null)
            return Unauthorized(new { message = "Invalid token" });

        var user = await _context.SystemUsers
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new ProfileDto
            {
                Id = x.Id,
                FullName = x.FullName,
                UserName = x.UserName,
                Email = x.Email,
                Department = x.Department,
                Role = x.Role,
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound(new { message = "Profile not found" });

        return Ok(user);
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = await _context.SystemUsers.FirstOrDefaultAsync(x => x.Id == _currentUser.Id);

        if (user == null)
            return Unauthorized(new { message = "Invalid token" });

        if (!PasswordHasher.Verify(request.CurrentPassword ?? "", user.Password, out _))
            return BadRequest(new { message = "Current password is incorrect" });

        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < PasswordHasher.MinLength)
            return BadRequest(new { message = $"New password must be at least {PasswordHasher.MinLength} characters" });

        if (request.NewPassword == request.CurrentPassword)
            return BadRequest(new { message = "New password must be different from the current password" });

        user.Password = PasswordHasher.Hash(request.NewPassword);
        _audit.Log(AuditActions.Update, user.UserName, "Changed own password");

        await _context.SaveChangesAsync();

        return Ok(new { message = "Password changed successfully" });
    }
}
