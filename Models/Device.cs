namespace AssetHub.Api.Models;

public class Device
{
    public int Id { get; set; }

    public string DeviceCode { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;

    public string? ImeiSerial { get; set; }
    public string? SerialNumber { get; set; }

    public string DeviceType { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    public string Ram { get; set; } = string.Empty;
    public string Storage { get; set; } = string.Empty;
    public string? Storage1 { get; set; }
    public string? Storage2 { get; set; }

    public string? Cpu { get; set; }
    public string? Gpu { get; set; }
    public int? Screens { get; set; }
    public bool Printer { get; set; } = false;
    public bool Scanner { get; set; } = false;

    public string Status { get; set; } = "Available";
    public string Notes { get; set; } = string.Empty;
    public string? InternalNotes { get; set; }
    public string? ImageUrl { get; set; }

    public int? AssignedUserId { get; set; }
    public AppUser? AssignedUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}