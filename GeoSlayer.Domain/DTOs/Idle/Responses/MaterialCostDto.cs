using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Progression.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Idle.Responses
{
    public class MaterialCostDto
    {
        public int MaterialId { get; set; }
        public required string Key { get; set; }
        public required string Name { get; set; }
        public int Quantity { get; set; }
        public long Held { get; set; }
    }
}
