using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Museum.Responses
{
    /// <summary>What a first-find produced, for the acquisition celebration.</summary>
    public class MuseumAcquisitionDto
    {
        public required string Key { get; set; }
        public required string Name { get; set; }
        public MuseumWing Wing { get; set; }
        public MuseumRarity Rarity { get; set; }
        public string? AcquiredAtName { get; set; }

        /// <summary>True when this filled the last empty plinth in its wing.</summary>
        public bool CompletedWing { get; set; }
    }
}
