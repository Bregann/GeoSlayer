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

    /// <summary>What grows wild — the Foraging ladder (Stage 04).</summary>
    Foraged,

    /// <summary>What is pulled from the water — the Fishing ladder (Stage 07).</summary>
    Caught,

    /// <summary>What is cut from trees — the Woodcutting ladder (Stage 08).</summary>
    Logged,

    /// <summary>Prepared food — the Cooking ladder (Stage 09). Feeds workers.</summary>
    Cooked,

    /// <summary>What is dug out of the ground — the Mining ladder (Stage 10).</summary>
    Mined,

    // ── Stage 15 ladders ──────────────────────────────────────────
    // One category per skill: two ladders sharing a category compete for the same tier
    // band and the roll picks between them arbitrarily.

    /// <summary>Farming's ladder.</summary>
    Grown,

    /// <summary>Trading's ladder.</summary>
    Traded,

    /// <summary>Prayer's ladder.</summary>
    Sacred,

    /// <summary>Knowledge's ladder.</summary>
    Written,

    /// <summary>Healing's ladder.</summary>
    Remedy,

    /// <summary>Athletics' ladder.</summary>
    Athletic,

    /// <summary>Tavern's ladder.</summary>
    Brewed,

    /// <summary>Banking's ladder.</summary>
    Coin,

    /// <summary>Combat's ladder.</summary>
    Martial,

    /// <summary>Worked metal — the Smithing ladder (Stage 11).</summary>
    Forged,

    /// <summary>
    /// The small reserved set of named materials from the rarest POIs (§7.4).
    ///
    /// Its own category rather than a terrain pool: these come from POI visits, not from
    /// walking over a cell, so they sit outside the terrain tier ladders entirely.
    /// </summary>
    Relic,
}
