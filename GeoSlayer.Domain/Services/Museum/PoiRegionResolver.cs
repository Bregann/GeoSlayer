using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Services.Museum;

/// <summary>
/// Names a region from POIs already imported nearby (Stage 12).
///
/// <para>Stage 12's design note is explicit that Nominatim's usage policy is restrictive
/// for bulk use, and prefers "deriving regions from the OSM data already imported" over
/// per-player API calls. This does exactly that: <b>no network call at all</b>.</para>
///
/// <para>The trade-off is honesty about resolution. This cannot name a county — it names
/// a locality from the POIs that are there. A cell with no named POI resolves to null and
/// simply adds no Cartography entry, which is better than inventing a place. If real
/// administrative boundaries are wanted later, swapping this for a self-hosted lookup is
/// a one-class change behind <see cref="IRegionResolver"/>.</para>
/// </summary>
public class PoiRegionResolver(AppDbContext db) : IRegionResolver
{
    /// <summary>Coarse cell size, matching the POI preloader's grid (~5 km).</summary>
    public const double RegionCellSize = 0.05;

    public static double SnapToGrid(double value) =>
        Math.Floor(value / RegionCellSize) * RegionCellSize;

    public async Task<RegionResult?> Resolve(double latitude, double longitude, CancellationToken ct)
    {
        var south = SnapToGrid(latitude);
        var west = SnapToGrid(longitude);

        // The most "notable" POI in the cell names it. Ordering by XpReward is a proxy
        // for notability that costs nothing — a cathedral outranks a corner shop, which
        // is the right instinct for naming a place.
        var anchor = await db.PointsOfInterest
            .Where(p => p.Location.Y >= south && p.Location.Y < south + RegionCellSize
                     && p.Location.X >= west && p.Location.X < west + RegionCellSize
                     && p.Name != "")
            .OrderByDescending(p => p.XpReward)
            .ThenBy(p => p.Id)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(anchor)) return null;

        // Keyed on the cell, not the name: two cells sharing a POI name are still two
        // different places, and a key collision would silently merge them.
        var key = $"region:{south:F2}:{west:F2}";

        return new RegionResult(key, anchor, "Locality");
    }
}
