namespace GeoSlayer.Domain.Enums
{
    /// <summary>
    /// Where an XP grant came from. Drives the Adventurer ratio: idle sources pay a
    /// reduced cut so a player who never leaves the house still progresses, just slowly
    /// (DESIGN.md §3.3).
    /// </summary>
    public enum XpSource
    {
        /// <summary>Revealing cells on foot — the everyday activity.</summary>
        Walk,

        /// <summary>Visiting a POI in range — the spike worth a detour.</summary>
        Poi,

        /// <summary>Workers trickling XP offline. Pays the reduced Adventurer ratio.</summary>
        Idle,

        /// <summary>Completing a craft.</summary>
        Craft,

        /// <summary>A discrete milestone grant (§3.0b) — already Adventurer XP, no cut applied.</summary>
        Milestone,
    }
}
