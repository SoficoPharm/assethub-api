namespace AssetHub.Api.Models;

public class Assignment
{
    public int Id { get; set; }

    public string AssignmentCode { get; set; } = string.Empty;

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public DateTime AssignedDate { get; set; } = DateTime.Now;
    public DateTime? ReturnedDate { get; set; }

    public string Status { get; set; } = "Active";
}