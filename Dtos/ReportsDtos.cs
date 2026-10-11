namespace AssetHub.Api.DTOs;

public class ReportsResponseDto
{
    public List<ReportChartItemDto> DevicesPerType { get; set; } = new();
    public List<ReportChartItemDto> DevicesPerDepartment { get; set; } = new();
    public AvailabilityStatusDto AvailabilityStatus { get; set; } = new();
}

public class ReportChartItemDto
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}

public class AvailabilityStatusDto
{
    public int TotalDevices { get; set; }
    public int Assigned { get; set; }
    public int Available { get; set; }
    public int Maintenance { get; set; }
    public int InactiveLost { get; set; }
}