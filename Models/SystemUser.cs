using AssetHub.Api.Authorization;

namespace AssetHub.Api.Models;

public class SystemUser
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = "123456";

    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Viewer;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}