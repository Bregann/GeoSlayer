using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Admin.Requests
{
    /// <summary>Create or update a Museum entry definition (Stage 18 task 8).</summary>
    public class SaveMuseumEntryRequest
    {
        /// <summary>Null when creating.</summary>
        public int? Id { get; set; }

        public required string Key { get; set; }
        public required string Name { get; set; }
        public string Description { get; set; } = "";

        public MuseumWing Wing { get; set; }
        public MuseumRarity Rarity { get; set; }

        /// <summary>What the empty plinth says.</summary>
        public required string UnlockCondition { get; set; }

        public int SortOrder { get; set; }
    }
}
