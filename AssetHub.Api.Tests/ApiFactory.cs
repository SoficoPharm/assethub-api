using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetHub.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Api.Tests;

/// <summary>
/// Hosts the real API in-process against a throwaway SQL Server database
/// (created and migrated before startup, dropped afterwards).
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SuperAdminUser = "test_superadmin";
    public const string SuperAdminPassword = "Test-Password-1";

    private readonly string _connectionString =
        $"Server=.;Database=AssetHubTests_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True;";

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_connectionString).Options);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync()
    {
        // Program.cs reads these while building the host, so they are set as environment variables.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _connectionString);
        Environment.SetEnvironmentVariable("Jwt__Key", new string('k', 48));
        Environment.SetEnvironmentVariable("SeedSuperAdmin__UserName", SuperAdminUser);
        Environment.SetEnvironmentVariable("SeedSuperAdmin__Password", SuperAdminPassword);

        // The SuperAdmin seeder runs at startup and needs the schema.
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await using (var db = CreateDbContext())
            await db.Database.EnsureDeletedAsync();

        await base.DisposeAsync();
    }

    /// <summary>An HttpClient signed in as the given user.</summary>
    public async Task<HttpClient> CreateClientAsync(string userName = SuperAdminUser, string password = SuperAdminPassword)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/Auth/login", new { userName, password });
        response.EnsureSuccessStatusCode();

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    private record LoginResponse(string Token);
}
