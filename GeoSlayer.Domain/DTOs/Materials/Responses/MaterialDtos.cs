using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Materials.Responses;

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

public class InventoryItemDto
{
    public int MaterialId { get; set; }
    public string Key { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int Tier { get; set; }
    public MaterialCategory Category { get; set; }
    public SkillType? SkillType { get; set; }
    public long Quantity { get; set; }
    public int StackCap { get; set; }
    public bool IsUnique { get; set; }

    /// <summary>At or above 90% of the cap — the app flags these before they overflow.</summary>
    public bool IsNearCap { get; set; }

    public bool IsFull { get; set; }
}

public class InventoryCategoryDto
{
    public MaterialCategory Category { get; set; }
    public string Name { get; set; } = null!;
    public List<InventoryItemDto> Items { get; set; } = [];
}

public class InventoryDto
{
    public List<InventoryCategoryDto> Categories { get; set; } = [];

    /// <summary>Distinct material types held — the headline number for the screen.</summary>
    public int DistinctMaterials { get; set; }

    public int NearCapCount { get; set; }
}
