using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Museum.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Clues.Responses
{
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
}
