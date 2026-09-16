namespace GeoSlayer.Domain.Enums;

/// <summary>
/// What a grid cell is made of (DESIGN.md §4.1).
///
/// A bitmask because terrain is not exclusive — a riverside wood is both
/// <see cref="Woodland"/> and <see cref="Water"/>, and a cell that yields from both pools
/// is correct rather than a special case.
///
/// <para><b>Terrain gates speed, never access</b> (§4.1a). It scales how much a cell
/// yields, but never which tiers are reachable — putting high tiers behind rare terrain
/// silently rebuilds the geographic lockout the unlock ladder exists to prevent.</para>
/// </summary>
[Flags]
public enum TerrainType
{
    /// <summary>
    /// Nothing identifiable. Deliberately not zero-yield: an unclassified cell still
    /// draws from the base pool, because a cell that yields nothing is a geographic
    /// dead zone.
    /// </summary>
    Open = 0,

    Woodland = 1 << 0,
    Water = 1 << 1,
    Farmland = 1 << 2,
    Urban = 1 << 3,
    Industrial = 1 << 4,
    Rocky = 1 << 5,
    Coastal = 1 << 6,
}
