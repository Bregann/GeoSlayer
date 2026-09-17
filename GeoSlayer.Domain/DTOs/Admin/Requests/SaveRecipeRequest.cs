using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Admin.Requests
{
    /// <summary>Create or update a recipe (Stage 18 task 7).</summary>
    public class SaveRecipeRequest
    {
        /// <summary>Null when creating.</summary>
        public int? Id { get; set; }

        public required string Key { get; set; }
        public required string Name { get; set; }
        public string Description { get; set; } = "";

        public SkillType SkillType { get; set; }
        public int LevelRequired { get; set; } = 1;
        public double DurationSeconds { get; set; } = 60;
        public double XpReward { get; set; }

        /// <summary>Exactly one of these is set — a recipe makes one thing.</summary>
        public int? OutputMaterialId { get; set; }
        public int? OutputItemId { get; set; }
        public int OutputQuantity { get; set; } = 1;

        public List<SaveRecipeInputRequest> Inputs { get; set; } = [];
    }

    /// <summary>One material a recipe consumes.</summary>
    public class SaveRecipeInputRequest
    {
        public int MaterialId { get; set; }
        public int Quantity { get; set; } = 1;
    }
}
