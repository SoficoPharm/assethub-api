namespace AssetHub.Api.Models;

public class UserImage
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.Now;
}