using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Retention.Responses
{
    public class PatrolCompletionDto
    {
        public int RouteId { get; set; }
        public required string Name { get; set; }
        public int UpkeepAwarded { get; set; }
        public List<MaterialGainDto> Materials { get; set; } = [];
    }
}
