using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Progression.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Idle.Responses
{
    public class SkillAccrualDto
    {
        public SkillType SkillType { get; set; }
        public string Name { get; set; } = null!;
        public long XpEarned { get; set; }
        public int Level { get; set; }
        public bool LevelledUp { get; set; }
    }
}
