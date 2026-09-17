using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Retention.Responses
{
    public class TransitRedemptionDto
    {
        public int Redeemed { get; set; }
        public int CellsRevealed { get; set; }
        public int Remaining { get; set; }

        /// <summary>Banked cells that lapsed before being walked.</summary>
        public int Expired { get; set; }
    }
}
