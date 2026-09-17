using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Museum.Responses
{
    public class DonationResultDto
    {
        public string Key { get; set; } = null!;
        public int Donated { get; set; }
        public long CurationEarned { get; set; }
        public long TotalCuration { get; set; }

        /// <summary>Always true: donating never removes the entry (§5A, criterion 6).</summary>
        public bool EntryRetained { get; set; } = true;
    }
}
