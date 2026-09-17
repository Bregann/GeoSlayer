using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Retention.Responses
{
    public class PatrolCompletionDto
    {
        public int RouteId { get; set; }
        public string Name { get; set; } = null!;
        public int UpkeepAwarded { get; set; }
        public List<MaterialGainDto> Materials { get; set; } = [];
    }
}
