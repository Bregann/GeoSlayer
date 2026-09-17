using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Materials.Responses
{
    /// <summary>Materials picked up by one sync, so the app can show the pickups.</summary>
    public class MaterialGainDto
    {
        public int MaterialId { get; set; }
        public string Key { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int Tier { get; set; }
        public MaterialCategory Category { get; set; }

        /// <summary>Units that actually landed in the inventory.</summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Units that could not fit and became Dust instead (§7.4). Non-zero here is the
        /// signal the app uses to warn that a stack is full.
        /// </summary>
        public int OverflowConvertedToDust { get; set; }
    }
}
