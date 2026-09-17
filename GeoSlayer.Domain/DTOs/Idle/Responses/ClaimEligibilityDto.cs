using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Progression.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Idle.Responses
{
    public class ClaimEligibilityDto
    {
        public bool IsEligible { get; set; }

        /// <summary>Why not, when <see cref="IsEligible"/> is false.</summary>
        public string? Reason { get; set; }

        /// <summary>How many of the 3×3 block's cells are revealed.</summary>
        public int RevealedCells { get; set; }
        public int RequiredCells { get; set; }

        /// <summary>Materials the claim would cost.</summary>
        public List<MaterialCostDto> Cost { get; set; } = [];

        public bool CanAfford { get; set; }

        /// <summary>Claims held against the density and slot caps.</summary>
        public int ClaimsHeld { get; set; }
        public int ClaimLimit { get; set; }
    }
}
