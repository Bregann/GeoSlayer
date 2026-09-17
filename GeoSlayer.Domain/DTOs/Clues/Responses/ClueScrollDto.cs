using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Museum.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Clues.Responses
{
    public class ClueScrollDto
    {
        public int Id { get; set; }
        public ClueTier Tier { get; set; }
        public required string TierName { get; set; }
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
}
