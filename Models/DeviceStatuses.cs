namespace AssetHub.Api.Models;

public static class DeviceStatuses
{
    public const string Available = "Available";
    public const string Assigned = "Assigned";
    public const string Maintenance = "Maintenance";
    public const string Inactive = "Inactive";
    public const string Lost = "Lost";

    public static readonly string[] All = { Available, Assigned, Maintenance, Inactive, Lost };

    /// <summary>Statuses that can be set directly. Assigned is only set through Assignments.</summary>
    public static readonly string[] Editable = { Available, Maintenance, Inactive, Lost };
}

public static class AssignmentStatuses
{
    public const string Active = "Active";
    public const string Returned = "Returned";
}
