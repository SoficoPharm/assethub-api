namespace AssetHub.DTOs
{
    public class DepartmentDto
    {
        public int Id { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string ManagerName { get; set; } = string.Empty;
        public int MembersCount { get; set; }
    }

    public class CreateDepartmentDto
    {
        public string DepartmentName { get; set; } = string.Empty;
        public string ManagerName { get; set; } = string.Empty;
        public int MembersCount { get; set; }
    }

    public class UpdateDepartmentDto
    {
        public string DepartmentName { get; set; } = string.Empty;
        public string ManagerName { get; set; } = string.Empty;
        public int MembersCount { get; set; }
    }
}