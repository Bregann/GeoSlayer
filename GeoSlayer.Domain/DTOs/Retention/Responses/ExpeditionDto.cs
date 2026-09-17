using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Retention.Responses
{
    public class ExpeditionDto
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public int PoiId { get; set; }
        public string PoiName { get; set; } = null!;
        public DateTime DispatchedUtc { get; set; }
        public DateTime ReturnsUtc { get; set; }
        public double DistanceMetres { get; set; }
        public bool HasReturned { get; set; }
        public double SecondsRemaining { get; set; }
    }
}
