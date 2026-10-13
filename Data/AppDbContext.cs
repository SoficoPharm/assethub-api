using AssetHub.Api.Models;
using AssetHub.Models;
using AssetHub.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AssetHub.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Department> Departments { get; set; }
    public DbSet<DeviceType> DeviceTypes { get; set; }
    public DbSet<SystemUser> SystemUsers { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>()
            .HasIndex(x => x.DeviceCode)
            .IsUnique();

        // Devices without a serial (NULL) are allowed; any non-null serial must be unique.
        // Values are stored normalized (trimmed, upper case) by DevicesController.
        modelBuilder.Entity<Device>()
            .HasIndex(x => x.SerialNumber)
            .IsUnique()
            .HasDatabaseName(SerialNumbers.UniqueIndexName)
            .HasFilter("[SerialNumber] IS NOT NULL");

        modelBuilder.Entity<Device>()
            .Property(x => x.ScreenSizeInches)
            .HasPrecision(4, 1);

        modelBuilder.Entity<SystemUser>()
            .HasIndex(x => x.UserName)
            .IsUnique();

        modelBuilder.Entity<Assignment>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Assignment>()
            .HasOne(x => x.Device)
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        // All timestamps are stored in UTC. SQL Server's datetime2 has no kind, so mark
        // values read back as UTC; JSON then serializes them with a trailing "Z".
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v,
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)))
        {
            property.SetValueConverter(utcConverter);
        }
    }
}