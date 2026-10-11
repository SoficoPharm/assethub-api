namespace AssetHub.Api.Models;

public class AppUser
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public int ImagesCount { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string UserDomain { get; set; } = string.Empty;
}