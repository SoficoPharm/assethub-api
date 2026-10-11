using AssetHub.Api.Data;
using AssetHub.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProfileController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var userName = User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(userName))
            return Unauthorized(new { message = "Invalid token" });

        var user = await _context.SystemUsers
            .AsNoTracking()
            .Where(x => x.UserName == userName)
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
}