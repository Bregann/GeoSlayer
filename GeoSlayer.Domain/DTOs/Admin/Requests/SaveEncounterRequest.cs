namespace GeoSlayer.Domain.DTOs.Admin.Requests
{
    /// <summary>Create or update an encounter definition (Stage 18 task 8).</summary>
    public class SaveEncounterRequest
    {
        /// <summary>Null when creating.</summary>
        public int? Id { get; set; }

        public required string Key { get; set; }
        public required string Name { get; set; }
        public string Description { get; set; } = "";

        public int Tier { get; set; } = 1;
        public int MinCombatLevel { get; set; } = 1;
        public bool IsTrainingGround { get; set; }
    }
}
