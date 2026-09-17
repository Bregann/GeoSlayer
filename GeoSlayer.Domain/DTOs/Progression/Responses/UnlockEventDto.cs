using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Progression.Responses
{
    /// <summary>A ladder rung the player just crossed.</summary>
    public class UnlockEventDto
    {
        public int AdventurerLevel { get; set; }
        public UnlockType UnlockType { get; set; }
        public required string Payload { get; set; }
        public required string DisplayName { get; set; }
    }
}
