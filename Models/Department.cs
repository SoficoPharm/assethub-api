using System.ComponentModel.DataAnnotations;

namespace AssetHub.Models
{
    public class Department
    {
        public int Id { get; set; }

        [Required]
        public string DepartmentName { get; set; } = string.Empty;

        public string ManagerName { get; set; } = string.Empty;

        public int MembersCount { get; set; }
    }
}