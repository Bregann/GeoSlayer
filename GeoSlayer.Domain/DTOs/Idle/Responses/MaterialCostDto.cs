using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Progression.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Idle.Responses
{
    public class MaterialCostDto
    {
        public int MaterialId { get; set; }
        public string Key { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int Quantity { get; set; }
        public long Held { get; set; }
    }
}
