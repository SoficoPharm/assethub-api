using AssetHub.Api.Models;
using AssetHub.Models;
using Microsoft.EntityFrameworkCore;

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
    }
}