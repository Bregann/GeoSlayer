namespace GeoSlayer.Domain.Enums;

/// <summary>
/// The shared pool a material belongs to (DESIGN.md §7.4).
///
/// Pools, not bespoke items: ~80 POI categories each dropping an exclusive would exceed 60
/// item types and be unmanageable on a phone. Most materials draw from a pool by category
/// and tier; only a small reserved set is unique.
/// </summary>
public enum MaterialCategory
{
    /// <summary>Universal filler and overflow currency. Every cell can yield it.</summary>
    Dust,

    Woodland,
    Water,
    Farmland,
    Urban,
    Industrial,
    Rocky,
    Coastal,

    /// <summary>
    /// The small reserved set of named materials from the rarest POIs (§7.4).
    ///
    /// Its own category rather than a terrain pool: these come from POI visits, not from
    /// walking over a cell, so they sit outside the terrain tier ladders entirely.
    /// </summary>
    Relic,
}
