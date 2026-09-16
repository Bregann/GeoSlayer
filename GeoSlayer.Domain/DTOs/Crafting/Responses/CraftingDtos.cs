using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Crafting.Responses;

/// <summary>
/// Why a recipe cannot be crafted. The distinction matters: §4.2 wants the player to know
/// the difference between "keep playing" and "go somewhere new".
/// </summary>
public enum RecipeLockReason
{
    /// <summary>Craftable now.</summary>
    None,

    /// <summary>The skill is not unlocked on the ladder yet.</summary>
    SkillLocked,

    /// <summary>The skill is unlocked but below the recipe's level.</summary>
    LevelLocked,

    /// <summary>
    /// Missing a material that only comes from a POI — genuinely unobtainable until the
    /// player travels, rather than a matter of playing longer.
    /// </summary>
    TravelGated,

    /// <summary>Missing ordinary materials the player can gather.</summary>
    MissingMaterials,
}

public class RecipeInputDto
{
    public int MaterialId { get; set; }
    public string Key { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int Quantity { get; set; }
    public long Held { get; set; }
    public bool HasEnough { get; set; }

    /// <summary>True when this input only comes from POI visits (§7.4 unique materials).</summary>
    public bool IsTravelGated { get; set; }
}

public class RecipeDto
{
    public string Key { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public SkillType SkillType { get; set; }
    public string SkillName { get; set; } = null!;
    public int LevelRequired { get; set; }
    public double DurationSeconds { get; set; }
    public double XpReward { get; set; }

    public string? OutputName { get; set; }
    public int OutputQuantity { get; set; }

    /// <summary>Gear, Tool or Building, when this recipe outputs an item.</summary>
    public ItemKind? OutputKind { get; set; }

    public List<RecipeInputDto> Inputs { get; set; } = [];

    public bool CanCraft { get; set; }
    public RecipeLockReason LockReason { get; set; }

    /// <summary>Human-readable reason, ready to render under a greyed recipe.</summary>
    public string? LockText { get; set; }
}

public class RecipeListDto
{
    public List<RecipeDto> Recipes { get; set; } = [];

    /// <summary>Crafts running now, against the queue limit.</summary>
    public int QueuedCount { get; set; }
    public int QueueLimit { get; set; }
}

public class CraftDto
{
    public int Id { get; set; }
    public string RecipeKey { get; set; } = null!;
    public string RecipeName { get; set; } = null!;
    public DateTime StartedUtc { get; set; }
    public DateTime CompletesUtc { get; set; }
    public bool IsComplete { get; set; }

    /// <summary>Seconds remaining, or 0 when done. For the queue timer.</summary>
    public double SecondsRemaining { get; set; }
}

/// <summary>What collecting finished crafts produced.</summary>
public class CraftCollectionDto
{
    public bool HasCollection { get; set; }
    public List<string> CompletedRecipes { get; set; } = [];
    public List<MaterialGainDto> Materials { get; set; } = [];
    public List<PlayerItemDto> Items { get; set; } = [];
    public long SkillXpEarned { get; set; }
    public long AdventurerXpEarned { get; set; }
}

public class PlayerItemDto
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string Key { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public ItemKind Kind { get; set; }
    public ItemSlot Slot { get; set; }
    public ItemModifier Modifier { get; set; }
    public double ModifierValue { get; set; }

    /// <summary>The effect as text, e.g. "+20% skill XP".</summary>
    public string ModifierText { get; set; } = null!;

    public int Tier { get; set; }
    public int Quantity { get; set; }
    public bool IsEquipped { get; set; }
    public int? ClaimId { get; set; }
    public string? ClaimName { get; set; }
}
