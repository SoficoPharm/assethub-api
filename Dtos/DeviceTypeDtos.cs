namespace AssetHub.Api.DTOs;

public class DeviceTypeDto
{
    public int Id { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public int DevicesCount { get; set; }
    public string Icon { get; set; } = string.Empty;
}

public class CreateDeviceTypeDto
{
    public string TypeName { get; set; } = string.Empty;
    public int DevicesCount { get; set; }
    public string Icon { get; set; } = string.Empty;
}

public class UpdateDeviceTypeDto
{
    public string TypeName { get; set; } = string.Empty;
    public int DevicesCount { get; set; }
    public string Icon { get; set; } = string.Empty;
}