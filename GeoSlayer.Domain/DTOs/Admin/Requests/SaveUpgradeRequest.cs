namespace GeoSlayer.Domain.DTOs.Admin.Requests
{
    /// <summary>Create or update a Bonus Point upgrade (Stage 18 task 8).</summary>
    public class SaveUpgradeRequest
    {
        /// <summary>Null when creating.</summary>
        public int? Id { get; set; }

        public required string Key { get; set; }
        public required string Name { get; set; }
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";

        public int MaxRank { get; set; } = 1;

        /// <summary>
        /// Comma-separated cost per rank.
        ///
        /// <para>Validated before saving: <c>UpgradeDefinition.Costs</c> parses this with
        /// <c>int.Parse</c> on every upgrades-screen load, so a malformed value would crash
        /// that screen for every player.</para>
        /// </summary>
        public required string CostCurve { get; set; }

        public double EffectPerRank { get; set; }
        public int MinAdventurerLevel { get; set; } = 1;
    }
}
