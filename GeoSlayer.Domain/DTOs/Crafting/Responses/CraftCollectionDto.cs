using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Crafting.Responses
{
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
}
