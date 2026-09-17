using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Museum.Responses
{
    /// <summary>One plinth, found or empty.</summary>
    public class MuseumEntryDto
    {
        public string Key { get; set; } = null!;
        public MuseumWing Wing { get; set; }
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public MuseumRarity Rarity { get; set; }

        /// <summary>Shown on an empty plinth, so a gap is a direction rather than a mystery.</summary>
        public string UnlockCondition { get; set; } = null!;

        public bool IsFound { get; set; }

        /// <summary>The diary: when it was first found. Null when the plinth is empty.</summary>
        public DateTime? FirstAcquiredUtc { get; set; }

        /// <summary>And where. "Found at Durham Cathedral, 3 May" is the entry.</summary>
        public string? AcquiredAtName { get; set; }

        public int Quantity { get; set; }

        /// <summary>Spares available to donate — total found, less what is already donated.</summary>
        public int DonatableQuantity { get; set; }
    }

    public class MuseumWingDto
    {
        public MuseumWing Wing { get; set; }
        public string Name { get; set; } = null!;
        public int Found { get; set; }
        public int Total { get; set; }
        public bool IsComplete { get; set; }

        /// <summary>
        /// What completing this wing grants, in words. Shown whether or not it is complete,
        /// so the reward is visible as a reason rather than a surprise.
        /// </summary>
        public string? SetBonusDescription { get; set; }

        /// <summary>True when the bonus is actually being applied.</summary>
        public bool SetBonusActive { get; set; }

        public List<MuseumEntryDto> Entries { get; set; } = [];
    }

    public class MuseumDto
    {
        public List<MuseumWingDto> Wings { get; set; } = [];
        public int TotalFound { get; set; }
        public int TotalEntries { get; set; }

        /// <summary>Museum-only currency from donated duplicates (§5A.1).</summary>
        public long Curation { get; set; }
    }

    /// <summary>What a first-find produced, for the acquisition celebration.</summary>
    public class MuseumAcquisitionDto
    {
        public string Key { get; set; } = null!;
        public string Name { get; set; } = null!;
        public MuseumWing Wing { get; set; }
        public MuseumRarity Rarity { get; set; }
        public string? AcquiredAtName { get; set; }

        /// <summary>True when this filled the last empty plinth in its wing.</summary>
        public bool CompletedWing { get; set; }
    }

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
