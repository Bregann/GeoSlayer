using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Museum.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Clues.Responses
{
    public class ClueStepDto
    {
        public int StepIndex { get; set; }
        public ClueStepType StepType { get; set; }
        public string RiddleText { get; set; } = null!;

        /// <summary>
        /// The search area for a coordinate step — never the exact answer.
        /// Null for other types, so the app cannot accidentally reveal a POI's position.
        /// </summary>
        public double? SearchLat { get; set; }
        public double? SearchLng { get; set; }
        public double? SearchRadius { get; set; }

        public bool IsSolved { get; set; }
        public bool WasSkipped { get; set; }
        public DateTime? SolvedUtc { get; set; }
    }

    public class ClueScrollDto
    {
        public int Id { get; set; }
        public ClueTier Tier { get; set; }
        public string TierName { get; set; } = null!;
        public int CurrentStep { get; set; }
        public int StepCount { get; set; }
        public bool IsComplete { get; set; }
        public bool SkipUsed { get; set; }
        public int SkipCost { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime? CompletedUtc { get; set; }

        /// <summary>
        /// Steps already solved, plus the current one. Future steps are withheld — reading
        /// ahead would let a player plan a route the clue is meant to reveal one leg at a time.
        /// </summary>
        public List<ClueStepDto> Steps { get; set; } = [];
    }

    /// <summary>What completing a step produced.</summary>
    public class ClueProgressDto
    {
        public int ScrollId { get; set; }
        public bool StepSolved { get; set; }
        public bool ScrollComplete { get; set; }
        public ClueStepDto? NextStep { get; set; }

        /// <summary>Only populated when the scroll finished.</summary>
        public ClueRewardDto? Reward { get; set; }
    }

    public class ClueRewardDto
    {
        public long CurationEarned { get; set; }
        public List<MaterialGainDto> Materials { get; set; } = [];

        /// <summary>Relics added to the Museum — the point of the whole system (§5B.4).</summary>
        public List<MuseumAcquisitionDto> Relics { get; set; } = [];
    }
}
