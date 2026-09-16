using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Interfaces.Api;

/// <summary>
/// Decides what terrain a grid cell is (Stage 03 task 3).
///
/// An interface so the classification source is swappable: the shipped implementation
/// reads already-imported OSM data, and tests substitute a fake rather than touching the
/// network. Nothing in the test suite should depend on a live Overpass call.
/// </summary>
public interface ITerrainClassifier
{
    /// <summary>
    /// Classify one grid cell. Implementations must never throw for an unknown cell —
    /// <see cref="TerrainType.Open"/> is the correct answer for "nothing identifiable",
    /// and it still yields base materials.
    /// </summary>
    Task<TerrainType> Classify(int gridLat, int gridLng, CancellationToken ct);
}
