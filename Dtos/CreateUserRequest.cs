using Microsoft.AspNetCore.Http;

namespace AssetHub.Api.DTOs;

/// <summary>
/// Multipart form. Optional fields are nullable: the UI sends "" for empty inputs,
/// which model binding turns into null, and a non-nullable string would make
/// [ApiController] reject the request as a missing required field.
/// </summary>
public class CreateUserRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? EmployeeId { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public int Images { get; set; }

    public string? ImageUrl { get; set; }
    public IFormFile? ImageFile { get; set; }
    public string? UserDomain { get; set; }
}