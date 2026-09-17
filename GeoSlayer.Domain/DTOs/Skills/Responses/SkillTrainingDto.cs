using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Skills.Responses
{
    /// <summary>XP a single skill gained from one sync or visit.</summary>
    public class SkillTrainingDto
    {
        public SkillType SkillType { get; set; }
        public string Name { get; set; } = null!;
        public long SkillXpEarned { get; set; }
        public long AdventurerXpEarned { get; set; }
        public int Level { get; set; }
        public bool LevelledUp { get; set; }
    }
}
