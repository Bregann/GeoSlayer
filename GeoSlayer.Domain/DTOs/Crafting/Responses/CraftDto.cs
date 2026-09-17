using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Crafting.Responses
{
    public class CraftDto
    {
        public int Id { get; set; }
        public required string RecipeKey { get; set; }
        public required string RecipeName { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime CompletesUtc { get; set; }
        public bool IsComplete { get; set; }

        /// <summary>Seconds remaining, or 0 when done. For the queue timer.</summary>
        public double SecondsRemaining { get; set; }
    }
}
