using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Progression.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Idle.Responses
{
    /// <summary>
    /// Worker upkeep for an accrual window (DESIGN.md §5.2).
    ///
    /// Unfed workers idle — nothing already earned is lost, because §7.4's "never punish you
    /// for sleeping" outranks the material sink.
    /// </summary>
    public class UpkeepDto
    {
        public int FoodRequired { get; set; }
        public int FoodConsumed { get; set; }

        /// <summary>True when the larder ran out. The app should nudge the player to cook.</summary>
        public bool Unfed { get; set; }

        public List<UpkeepLineDto> Consumed { get; set; } = [];
    }

    /// <summary>One food material eaten as upkeep.</summary>
    public class UpkeepLineDto
    {
        public int MaterialId { get; set; }
        public required string Name { get; set; }
        public int Quantity { get; set; }
    }
}
