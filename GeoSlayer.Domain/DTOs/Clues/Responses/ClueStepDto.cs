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
}
