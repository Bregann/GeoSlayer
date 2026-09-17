using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Admin.Responses
{
    /// <summary>A material as the admin interface sees it (Stage 18 task 6).</summary>
    public class AdminMaterialDto
    {
        public int Id { get; set; }
        public required string Key { get; set; }
        public required string Name { get; set; }

        public MaterialCategory Category { get; set; }
        public SkillType? SkillType { get; set; }
        public int Tier { get; set; }
        public int LevelRequired { get; set; }
        public double BaseGatherSeconds { get; set; }
        public double XpPerUnit { get; set; }
        public bool IsUnique { get; set; }

        /// <summary>
        /// What this sells for, derived from tier and category (§5D.1).
        ///
        /// <para>Shown because it is not editable directly — it falls out of the tier and
        /// category chosen above, and an admin should see the consequence rather than
        /// discover it.</para>
        /// </summary>
        public long UnitPrice { get; set; }

        /// <summary>XP per gathering second, the number §4.1a actually constrains.</summary>
        public double XpPerSecond { get; set; }
    }
}
