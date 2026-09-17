using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Crafting.Responses
{
    public class RecipeListDto
    {
        public List<RecipeDto> Recipes { get; set; } = [];

        /// <summary>Crafts running now, against the queue limit.</summary>
        public int QueuedCount { get; set; }
        public int QueueLimit { get; set; }
    }

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
        public required string Key { get; set; }
        public required string Name { get; set; }
        public int Quantity { get; set; }
        public long Held { get; set; }
        public bool HasEnough { get; set; }

        /// <summary>True when this input only comes from POI visits (§7.4 unique materials).</summary>
        public bool IsTravelGated { get; set; }
    }

    public class RecipeDto
    {
        public required string Key { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public SkillType SkillType { get; set; }
        public required string SkillName { get; set; }
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
}
