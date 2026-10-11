using Microsoft.AspNetCore.Http;

namespace AssetHub.Api.DTOs;

public class CreateDeviceRequest
{
    public string DeviceName { get; set; } = string.Empty;

    public string? ImeiSerial { get; set; }
    public string? SerialNumber { get; set; }

    public string DeviceType { get; set; } = string.Empty;
    public string? Platform { get; set; }

    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Ram { get; set; } = string.Empty;

    public string? Storage { get; set; }
    public string? Storage1 { get; set; }
    public string? Storage2 { get; set; }

    public string? Cpu { get; set; }
    public string? Gpu { get; set; }
    public int? Screens { get; set; }
    public bool Printer { get; set; }
    public bool Scanner { get; set; }

    public string Status { get; set; } = "Available";
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }

    public string? ImageUrl { get; set; }
    public IFormFile? ImageFile { get; set; }
}