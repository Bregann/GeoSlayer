using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Clues;

/// <summary>
/// Per-tier shape for clue scrolls (DESIGN.md §5B.1).
///
/// <para>Tiers scale step count, travel distance and reward quality. The radii are
/// deliberately modest: §5B.3 forbids a step "requiring a 200-mile trip", and an Odyssey
/// is meant to be a weekend walk, not a holiday.</para>
/// </summary>
public static class ClueTierConfig
{
    public readonly record struct TierShape(
        int StepCount,
        double SearchRadiusMetres,
        int CurationReward,
        int SkipCostCuration);

    private static readonly Dictionary<ClueTier, TierShape> Shapes = new()
    {
        // Radius is how far from the player's own territory a step may be placed. Even
        // Odyssey stays inside a comfortable day's travel.
        [ClueTier.Wandering] = new(2, 1_500, 25, 5),
        [ClueTier.Roaming] = new(3, 4_000, 60, 10),
        [ClueTier.Pilgrim] = new(4, 10_000, 150, 20),
        [ClueTier.Odyssey] = new(5, 25_000, 400, 40),
    };

    public static TierShape For(ClueTier tier) => Shapes[tier];

    /// <summary>
    /// Metres within which arrival counts for a coordinate step.
    ///
    /// Generous on purpose: a pin is a search *area*, and demanding GPS precision would
    /// make the step a frustration rather than a hunt.
    /// </summary>
    public const double CoordinateArrivalRadiusMetres = 120;

    /// <summary>
    /// Metres within which a POI step counts as reached. Matches the POI interact radius
    /// plus its sync-staleness grace, so a clue is no harder to complete than a visit.
    /// </summary>
    public const double PoiArrivalRadiusMetres = 80;
}
