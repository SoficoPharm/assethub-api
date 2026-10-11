using Microsoft.AspNetCore.Http;

namespace AssetHub.Api.DTOs;

/// <summary>Multipart form. Optional fields are nullable; see CreateUserRequest.</summary>
public class CreateDeviceRequest
{
    public string? DeviceName { get; set; }

    public string? ImeiSerial { get; set; }
    public string? SerialNumber { get; set; }

    public string? DeviceType { get; set; }
    public string? Platform { get; set; }

    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Ram { get; set; }

    public string? Storage { get; set; }
    public string? Storage1 { get; set; }
    public string? Storage2 { get; set; }

    public string? Cpu { get; set; }
    public string? Gpu { get; set; }
    public int? Screens { get; set; }
    public bool Printer { get; set; }
    public bool Scanner { get; set; }

    public string? Status { get; set; }
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }

    public string? ImageUrl { get; set; }
    public IFormFile? ImageFile { get; set; }
}