using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Progression.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Idle.Responses
{
    /// <summary>
    /// Worker upkeep for an accrual window (DESIGN.md §5.2).
    ///
    /// Two costs, not two currencies for one cost: workers are <b>paid in coin and fed on
    /// top</b>. Unpaid or unfed workers idle — nothing already earned is lost, because §7.4's
    /// "never punish you for sleeping" outranks the sink.
    /// </summary>
    public class UpkeepDto
    {
        public int FoodRequired { get; set; }
        public int FoodConsumed { get; set; }

        /// <summary>True when the larder ran out. The app should nudge the player to cook.</summary>
        public bool Unfed { get; set; }

        /// <summary>Coin owed in wages for the window (§5.2).</summary>
        public long WagesRequired { get; set; }

        /// <summary>
        /// Coin actually paid.
        ///
        /// <para>May be less than <see cref="WagesRequired"/>: partial payment is deliberate,
        /// because taking nothing would let a player run workers indefinitely on an empty
        /// purse, and taking them into debt would punish them for sleeping.</para>
        /// </summary>
        public long WagesPaid { get; set; }

        /// <summary>True when the purse ran out. The app should nudge the player to sell.</summary>
        public bool Unpaid { get; set; }

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
