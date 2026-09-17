using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Skills.Responses
{
    /// <summary>
    /// The outcome of a POI visit (Stage 04 task 2).
    ///
    /// Shaped as a <b>session</b> rather than a one-shot result: §3.1d plans POI minigames,
    /// and a session token means those can grow into this endpoint without a rewrite.
    /// </summary>
    public class PoiVisitResultDto
    {
        /// <summary>Identifies this visit, for a future minigame to continue against.</summary>
        public Guid SessionToken { get; set; }

        public int PoiId { get; set; }
        public required string PoiName { get; set; }
        public SkillType Skill { get; set; }

        public long SkillXpEarned { get; set; }
        public long AdventurerXpEarned { get; set; }
        public int SkillLevel { get; set; }
        public bool LevelledUp { get; set; }

        /// <summary>True the first time this player ever visits this POI.</summary>
        public bool IsFirstVisit { get; set; }

        /// <summary>Decay-relevant visit count after this visit (§3.4).</summary>
        public int VisitCount { get; set; }

        /// <summary>Lifetime visits, never decayed.</summary>
        public int TotalVisits { get; set; }

        /// <summary>
        /// The decay multiplier applied, 0.05–1.0. Surfaced so the app can explain a reduced
        /// reward rather than leaving the player to guess why the number shrank.
        /// </summary>
        public double DecayMultiplier { get; set; }

        public List<MaterialGainDto> Materials { get; set; } = [];

        /// <summary>Ladder rungs crossed by this visit's XP.</summary>
        public List<DTOs.Progression.Responses.UnlockEventDto> Unlocks { get; set; } = [];

        /// <summary>Museum plinths filled by this visit (Stage 12).</summary>
        public List<DTOs.Museum.Responses.MuseumAcquisitionDto> MuseumAcquisitions { get; set; } = [];
    }
}
