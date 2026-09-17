using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Museum.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Clues.Responses
{
    public class ClueRewardDto
    {
        public long CurationEarned { get; set; }
        public List<MaterialGainDto> Materials { get; set; } = [];

        /// <summary>Relics added to the Museum — the point of the whole system (§5B.4).</summary>
        public List<MuseumAcquisitionDto> Relics { get; set; } = [];
    }
}
