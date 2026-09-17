namespace GeoSlayer.Domain.DTOs.Economy.Requests
{
    /// <summary>What to sell, and where (DESIGN.md §5.4).</summary>
    public class SellRequest
    {
        /// <summary>The shop. Its position is checked against the server's own fix (§7.2).</summary>
        public int PoiId { get; set; }

        /// <summary>Quantity per material id. Selling more than held sells what is held.</summary>
        public required Dictionary<int, long> Quantities { get; set; }
    }
}
