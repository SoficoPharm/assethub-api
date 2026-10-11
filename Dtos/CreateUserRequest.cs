using Microsoft.AspNetCore.Http;

namespace AssetHub.Api.DTOs;

public class CreateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public int Images { get; set; }

    public string? ImageUrl { get; set; }
    public IFormFile? ImageFile { get; set; }
    public string? UserDomain { get; set; }
}