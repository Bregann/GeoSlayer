using NetTopologySuite.Geometries;

namespace GeoSlayer.Domain.Interfaces.Api;

/// <summary>
/// Provides spatial queries for player-to-world proximity checks.
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// Determines whether a player position is within a given distance of a specific street.
    /// </summary>
    /// <param name="playerLocation">The player's current GPS point (SRID 4326).</param>
    /// <param name="streetId">The database ID of the street to check against.</param>
    /// <param name="thresholdMeters">Maximum allowed distance in meters (default 10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the player is within range; otherwise <c>false</c>.</returns>
    Task<bool> IsPlayerNearStreetAsync(
        Point playerLocation,
        int streetId,
        double thresholdMeters = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the nearest street within threshold distance of the player.
    /// </summary>
    /// <returns>Street Id and Name, or null if none in range.</returns>
    Task<(int StreetId, string StreetName)?> FindNearestStreetAsync(
        Point playerLocation,
        double thresholdMeters = 15,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the fraction (0–1) along the street LineString closest to the player.
    /// Uses PostGIS ST_LineLocatePoint.
    /// </summary>
    Task<double> CalculateStreetFractionAsync(
        int streetId,
        Point playerLocation,
        CancellationToken cancellationToken = default);
}
