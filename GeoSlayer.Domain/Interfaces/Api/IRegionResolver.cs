namespace GeoSlayer.Domain.Interfaces.Api;

/// <summary>
/// Names the region containing a coordinate (Stage 12 Cartography).
///
/// <para>An interface for the same reason as <c>ITerrainClassifier</c>: the source is
/// swappable, and <b>no test may depend on a third-party lookup</b>. Stage 12's design
/// note rules out per-player Nominatim calls, so the shipped implementation derives
/// regions from OSM data already imported.</para>
/// </summary>
public interface IRegionResolver
{
    /// <summary>
    /// Resolve a region, or null when nothing can be determined.
    ///
    /// Null is an acceptable answer — an unnamed region simply adds no Cartography entry,
    /// which is better than inventing a place name.
    /// </summary>
    Task<RegionResult?> Resolve(double latitude, double longitude, CancellationToken ct);
}

/// <summary>A resolved region.</summary>
public record RegionResult(string RegionKey, string Name, string Kind);
