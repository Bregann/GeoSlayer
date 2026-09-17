namespace GeoSlayer.Domain.Enums
{
    /// <summary>
    /// The Museum's wings (DESIGN.md §5A.2).
    ///
    /// <para>Each is filled from data the game already produces — the point of §5A.2 is that
    /// the entries "fall out of existing data" rather than needing new content authoring.</para>
    /// </summary>
    public enum MuseumWing
    {
        /// <summary>
        /// Regions, counties and countries entered. §5A.2 calls this "the one that makes the
        /// Museum yours" — "7 of 48 counties" reads in a way an item list does not.
        /// </summary>
        Cartography,

        /// <summary>POI types stood at, straight from the OSM tag table.</summary>
        Landmarks,

        /// <summary>Terrain types traversed, from cell classification.</summary>
        Naturalist,

        /// <summary>Materials gathered and items crafted, per skill and tier.</summary>
        Skills,

        /// <summary>Low-drop-rate finds.</summary>
        Rarities,

        /// <summary>Milestones derived from existing counters.</summary>
        Feats,

        /// <summary>Clue scroll rewards. Defined but empty until Stage 13.</summary>
        Relics,

        /// <summary>Trophies from distant POIs. Defined but empty until Stage 14.</summary>
        Expeditions,
    }
}
