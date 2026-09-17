namespace GeoSlayer.Domain.DTOs.Admin.Responses
{
    /// <summary>An encounter definition as the admin interface sees it (Stage 18 task 8).</summary>
    public class AdminEncounterDto
    {
        public int Id { get; set; }
        public required string Key { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }

        public int Tier { get; set; }
        public int MinCombatLevel { get; set; }

        /// <summary>Permanent, fixed to historic ground — the reliable route (§5C.1).</summary>
        public bool IsTrainingGround { get; set; }

        /// <summary>
        /// Warnings about the whole set, repeated on every row.
        ///
        /// <para>Set-level rather than per-encounter because that is what §5C.2 constrains —
        /// a single definition is never wrong on its own, only in relation to the others.</para>
        /// </summary>
        public List<string> SetWarnings { get; set; } = [];
    }
}
