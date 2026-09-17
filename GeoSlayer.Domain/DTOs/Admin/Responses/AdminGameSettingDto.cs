namespace GeoSlayer.Domain.DTOs.Admin.Responses
{
    /// <summary>A tunable number as the admin interface sees it (Stage 18).</summary>
    public class AdminGameSettingDto
    {
        public int Id { get; set; }
        public required string Key { get; set; }
        public required string Value { get; set; }

        /// <summary>What it shipped as, so drift from the tuned balance is visible.</summary>
        public required string Default { get; set; }

        public required string Category { get; set; }
        public required string Description { get; set; }

        public double MinValue { get; set; }
        public double MaxValue { get; set; }

        /// <summary>True when the current value differs from what shipped.</summary>
        public bool IsChanged { get; set; }
    }
}
