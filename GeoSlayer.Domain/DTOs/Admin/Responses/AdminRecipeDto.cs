using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Admin.Responses
{
    /// <summary>A recipe as the admin interface sees it (Stage 18 task 7).</summary>
    public class AdminRecipeDto
    {
        public int Id { get; set; }
        public required string Key { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }

        public SkillType SkillType { get; set; }
        public int LevelRequired { get; set; }
        public double DurationSeconds { get; set; }
        public double XpReward { get; set; }

        public int? OutputMaterialId { get; set; }
        public int? OutputItemId { get; set; }
        public int OutputQuantity { get; set; }

        /// <summary>What it makes, named — the chain is unreadable as bare ids.</summary>
        public string? OutputName { get; set; }

        public List<AdminRecipeInputDto> Inputs { get; set; } = [];

        /// <summary>Coin value of everything consumed (§5D.1).</summary>
        public long InputCost { get; set; }

        /// <summary>Coin value of what comes out.</summary>
        public long OutputValue { get; set; }

        /// <summary>
        /// Things worth knowing, none of which blocked the save.
        ///
        /// <para>A loss-making recipe is the headline one: §5D.1 expects produced goods to
        /// beat their parts, but an admin mid-tune must be able to save something
        /// temporarily unattractive.</para>
        /// </summary>
        public List<string> Warnings { get; set; } = [];
    }

    /// <summary>One material a recipe consumes.</summary>
    public class AdminRecipeInputDto
    {
        public int MaterialId { get; set; }
        public required string MaterialKey { get; set; }
        public required string MaterialName { get; set; }
        public int Quantity { get; set; }
    }
}
