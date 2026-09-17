using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Progression.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Idle.Responses
{
    /// <summary>
    /// The welcome-back payload (Stage 05 task 4) — what accrued while away.
    /// </summary>
    public class OfflineAccrualDto
    {
        /// <summary>False when nothing meaningful accrued, so the screen stays quiet.</summary>
        public bool HasAccrual { get; set; }

        public double HoursAccrued { get; set; }

        /// <summary>True when the cap was hit, so the app can suggest a longer cap.</summary>
        public bool WasCapped { get; set; }

        public double OfflineCapHours { get; set; }

        public List<MaterialGainDto> Materials { get; set; } = [];
        public List<SkillAccrualDto> Skills { get; set; } = [];
        public List<UnlockEventDto> Unlocks { get; set; } = [];

        public long AdventurerXpEarned { get; set; }
        public int BonusPointsGranted { get; set; }

        /// <summary>Food eaten while you were away (§5.2).</summary>
        public UpkeepDto UpkeepConsumed { get; set; } = new();

        /// <summary>True when workers ran out of food — a nudge to cook, not a penalty.</summary>
        public bool WorkersWentUnfed { get; set; }

        /// <summary>
        /// True when the purse ran dry and workers went unpaid (§5.2).
        ///
        /// <para>The nudge that tells a player to sell a haul, the way
        /// <see cref="WorkersWentUnfed"/> tells them to cook.</para>
        /// </summary>
        public bool WorkersWentUnpaid { get; set; }
    }
}
