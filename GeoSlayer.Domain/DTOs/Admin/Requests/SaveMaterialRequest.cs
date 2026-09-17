using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Admin.Requests
{
    /// <summary>Create or update a material (Stage 18 task 6).</summary>
    public class SaveMaterialRequest
    {
        /// <summary>Null when creating.</summary>
        public int? Id { get; set; }

        public required string Key { get; set; }
        public required string Name { get; set; }

        public MaterialCategory Category { get; set; }
        public SkillType? SkillType { get; set; }
        public int Tier { get; set; } = 1;
        public int LevelRequired { get; set; } = 1;
        public double BaseGatherSeconds { get; set; } = 3;
        public double XpPerUnit { get; set; } = 5;
        public bool IsUnique { get; set; }
    }
}
