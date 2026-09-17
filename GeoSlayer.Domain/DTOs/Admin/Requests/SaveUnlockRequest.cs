using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Admin.Requests
{
    /// <summary>Create or update one rung of the unlock ladder (Stage 18 task 8).</summary>
    public class SaveUnlockRequest
    {
        /// <summary>Null when creating.</summary>
        public int? Id { get; set; }

        public int AdventurerLevel { get; set; } = 1;
        public UnlockType UnlockType { get; set; }

        /// <summary>The skill or system granted — a SkillType name, or a system key.</summary>
        public required string Payload { get; set; }

        /// <summary>What the unlock celebration shows.</summary>
        public required string DisplayName { get; set; }
    }
}
