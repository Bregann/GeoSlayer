namespace GeoSlayer.Domain.DTOs.Admin.Responses
{
    /// <summary>One line of the audit trail (Stage 18 task 9).</summary>
    public class AdminAuditDto
    {
        public int Id { get; set; }
        public required string AdminUsername { get; set; }
        public required string EntityType { get; set; }
        public string? EntityId { get; set; }
        public required string Action { get; set; }
        public string? Detail { get; set; }
        public DateTime OccurredUtc { get; set; }
    }
}
