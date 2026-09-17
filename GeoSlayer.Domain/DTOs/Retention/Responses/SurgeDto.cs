using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Retention.Responses
{
    public class SurgeDto
    {
        public int Id { get; set; }
        public required string Description { get; set; }
        public TerrainType? TargetTerrain { get; set; }
        public SkillType? TargetSkill { get; set; }
        public double Multiplier { get; set; }
        public DateTime EndsUtc { get; set; }
    }
}
