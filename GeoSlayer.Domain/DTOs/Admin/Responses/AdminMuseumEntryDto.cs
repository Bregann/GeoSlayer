using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Admin.Responses
{
    /// <summary>A Museum entry definition as the admin interface sees it (Stage 18 task 8).</summary>
    public class AdminMuseumEntryDto
    {
        public int Id { get; set; }
        public required string Key { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }

        public MuseumWing Wing { get; set; }
        public MuseumRarity Rarity { get; set; }

        /// <summary>What the empty plinth says — §5A.1 rests on this reading as a pull.</summary>
        public required string UnlockCondition { get; set; }

        public int SortOrder { get; set; }

        /// <summary>How many players have found it, for judging whether it is too rare.</summary>
        public int FoundBy { get; set; }

        /// <summary>True when artwork has been uploaded for this entry.</summary>
        public bool HasSprite { get; set; }

        /// <summary>Warnings about the set as a whole, repeated on every row.</summary>
        public List<string> SetWarnings { get; set; } = [];
    }
}
