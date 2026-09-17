namespace GeoSlayer.Domain.DTOs.Economy.Responses
{
    /// <summary>The outcome of selling at a shop (DESIGN.md §5.4).</summary>
    public class SellResultDto
    {
        /// <summary>Coin earned by this sale, sell-price bonus included.</summary>
        public long CoinEarned { get; set; }

        /// <summary>The player's coin in hand afterwards.</summary>
        public long CoinBalance { get; set; }

        /// <summary>What was sold, so the app can say more than a number.</summary>
        public List<SoldMaterialDto> Sold { get; set; } = [];

        /// <summary>The shop it was sold at — named, because the walk there was the cost.</summary>
        public required string PoiName { get; set; }
    }

    /// <summary>One line of a sale.</summary>
    public class SoldMaterialDto
    {
        public int MaterialId { get; set; }
        public required string Key { get; set; }
        public required string Name { get; set; }
        public long Quantity { get; set; }
        public long Coin { get; set; }
    }
}
