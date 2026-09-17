using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Materials.Responses
{
    public class InventoryDto
    {
        public List<InventoryCategoryDto> Categories { get; set; } = [];

        /// <summary>Distinct material types held — the headline number for the screen.</summary>
        public int DistinctMaterials { get; set; }

        /// <summary>What the whole inventory would fetch at a shop, bonus included.</summary>
        public long TotalSellValue { get; set; }
    }

    public class InventoryItemDto
    {
        public int MaterialId { get; set; }
        public required string Key { get; set; }
        public required string Name { get; set; }
        public int Tier { get; set; }
        public MaterialCategory Category { get; set; }
        public SkillType? SkillType { get; set; }
        public long Quantity { get; set; }
        public bool IsUnique { get; set; }

        /// <summary>Coin for one unit, before the player's sell bonus (§5.4).</summary>
        public long UnitPrice { get; set; }

        /// <summary>Coin the whole stack fetches, bonus included.</summary>
        public long StackPrice { get; set; }

        /// <summary>Safe for the "sell all junk" shortcut — low tier, never a Relic.</summary>
        public bool IsJunk { get; set; }

    }

    public class InventoryCategoryDto
    {
        public MaterialCategory Category { get; set; }
        public required string Name { get; set; }
        public List<InventoryItemDto> Items { get; set; } = [];
    }
}
