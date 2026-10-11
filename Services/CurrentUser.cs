using System.Security.Claims;

namespace AssetHub.Api.Services;

public interface ICurrentUser
{
    int? Id { get; }
    string? UserName { get; }
    string? Role { get; }

    /// <summary>Full name if present in the token, otherwise the user name.</summary>
    string DisplayName { get; }
}

public class CurrentUser : ICurrentUser
{
    public const string FullNameClaim = "full_name";

    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public int? Id =>
        int.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? UserName => Principal?.FindFirstValue(ClaimTypes.Name);

    public string? Role => Principal?.FindFirstValue(ClaimTypes.Role);

    public string DisplayName
    {
        get
        {
            var fullName = Principal?.FindFirstValue(FullNameClaim);
            if (!string.IsNullOrWhiteSpace(fullName))
                return fullName;

            return UserName ?? "Unknown";
        }
    }
}
