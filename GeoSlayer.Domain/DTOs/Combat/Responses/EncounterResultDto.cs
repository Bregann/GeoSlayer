using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Museum.Responses;
using GeoSlayer.Domain.DTOs.Progression.Responses;

namespace GeoSlayer.Domain.DTOs.Combat.Responses
{
    /// <summary>The outcome of a fight (DESIGN.md §5C.3).</summary>
    public class EncounterResultDto
    {
        public int EncounterId { get; set; }
        public required string Name { get; set; }
        public bool Won { get; set; }

        public long SkillXpEarned { get; set; }
        public long AdventurerXpEarned { get; set; }
        public int CombatLevel { get; set; }
        public bool LevelledUp { get; set; }

        /// <summary>Empty on a loss — losing costs time, never materials (§5C.3).</summary>
        public List<MaterialGainDto> Materials { get; set; } = [];

        public List<UnlockEventDto> Unlocks { get; set; } = [];
        public List<MuseumAcquisitionDto> MuseumAcquisitions { get; set; } = [];

        /// <summary>What the player is told. A loss must not read as a penalty.</summary>
        public required string Message { get; set; }
    }
}
