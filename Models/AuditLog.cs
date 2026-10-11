namespace AssetHub.Api.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string Actor { get; set; } = string.Empty;

        public string LogAction { get; set; } = string.Empty;

        public string Target { get; set; } = string.Empty;

        public string Details { get; set; } = string.Empty;
    }
}