using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Retention.Responses
{
    public class ExpeditionCollectionDto
    {
        public bool HasCollection { get; set; }
        public List<string> Returned { get; set; } = [];
        public List<MaterialGainDto> Materials { get; set; } = [];
        public long SkillXpEarned { get; set; }
    }
}
