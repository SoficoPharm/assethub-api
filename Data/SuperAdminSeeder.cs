using AssetHub.Api.Authorization;
using AssetHub.Api.Models;
using AssetHub.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Api.Data;

/// <summary>
/// Creates the first SuperAdmin on startup when none exists, from
/// SeedSuperAdmin:UserName / SeedSuperAdmin:Password (appsettings.Development.json
/// or environment variables SeedSuperAdmin__UserName / SeedSuperAdmin__Password).
/// </summary>
public static class SuperAdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SuperAdminSeeder));

        try
        {
            if (await context.SystemUsers.AnyAsync(x => x.Role == Roles.SuperAdmin))
                return;

            var userName = config["SeedSuperAdmin:UserName"]?.Trim();
            var password = config["SeedSuperAdmin:Password"];

            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
            {
                logger.LogWarning(
                    "No SuperAdmin exists and SeedSuperAdmin:UserName / SeedSuperAdmin:Password are not configured. " +
                    "Nobody can manage system users until one is created.");
                return;
            }

            if (password.Length < PasswordHasher.MinLength)
            {
                logger.LogError("SeedSuperAdmin:Password must be at least {MinLength} characters. SuperAdmin not created.",
                    PasswordHasher.MinLength);
                return;
            }

            if (await context.SystemUsers.AnyAsync(x => x.UserName == userName))
            {
                logger.LogError(
                    "Cannot seed SuperAdmin: user name '{UserName}' already exists with another role. " +
                    "Choose a different SeedSuperAdmin:UserName.", userName);
                return;
            }

            context.SystemUsers.Add(new SystemUser
            {
                FullName = "Super Admin",
                UserName = userName,
                Password = PasswordHasher.Hash(password),
                Role = Roles.SuperAdmin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();

            logger.LogWarning(
                "Created SuperAdmin '{UserName}' from SeedSuperAdmin config. " +
                "Log in and change this password now, then remove SeedSuperAdmin:Password from config.", userName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SuperAdmin seeding failed. Has `dotnet ef database update` been run?");
        }
    }
}
