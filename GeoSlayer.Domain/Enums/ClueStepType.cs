namespace GeoSlayer.Domain.Enums
{
    /// <summary>
    /// How a clue step names its destination (DESIGN.md §5B.2).
    ///
    /// <para>Every type is answerable from OSM data already imported, which is what makes the
    /// system "tractable rather than a content-authoring treadmill". Stage 13 ships the
    /// simplest three; the rest are defined so the schema does not need changing to add them.</para>
    /// </summary>
    public enum ClueStepType
    {
        /// <summary>A named POI, validated by id. "Visit the church on Mill Lane."</summary>
        Direct,

        /// <summary>A POI category — works anywhere. "Stand at any lighthouse."</summary>
        Category,

        /// <summary>A pin with a search radius, validated by position.</summary>
        Coordinate,

        /// <summary>Terrain, from Stage 03 classification. Not yet generated.</summary>
        Terrain,

        /// <summary>Template plus POI tags. Not yet generated — see Stage 13 task 3.</summary>
        Cryptic,

        /// <summary>A spatial query. "Where two rivers meet." Not yet generated.</summary>
        Relational,

        /// <summary>Position plus bearing from a prior point. Not yet generated.</summary>
        Sequence,
    }
}
