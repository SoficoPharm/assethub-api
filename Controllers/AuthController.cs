using AssetHub.Api.Data;
using AssetHub.Api.DTOs;
using AssetHub.Api.Models;
using AssetHub.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(8);

    private readonly AppDbContext _context;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _context.SystemUsers
            .FirstOrDefaultAsync(x => x.UserName == request.UserName);

        if (user == null || !PasswordHasher.Verify(request.Password, user.Password, out var needsRehash))
            return Unauthorized(new { message = "Invalid username or password" });

        if (!user.IsActive)
            return Unauthorized(new { message = "User is inactive" });

        // Lazy migration: legacy plaintext passwords are hashed on first successful login.
        if (needsRehash)
        {
            user.Password = PasswordHasher.Hash(request.Password);
            await _context.SaveChangesAsync();
        }

        var token = GenerateToken(user);

        return Ok(new
        {
            token,
            user = new
            {
                user.Id,
                user.FullName,
                user.UserName,
                user.Role
            }
        });
    }

    private string GenerateToken(SystemUser user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(CurrentUser.FullNameClaim, user.FullName),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!)
        );

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.Add(TokenLifetime),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
