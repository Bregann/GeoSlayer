using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Interfaces.Api;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace GeoSlayer.Domain.Services;

/// <inheritdoc />
public class LocationService(AppDbContext db) : ILocationService
{
    /// <inheritdoc />
    public async Task<bool> IsPlayerNearStreetAsync(
        Point playerLocation,
        int streetId,
        double thresholdMeters = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playerLocation);

        var isNear = await db.Streets
            .Where(s => s.Id == streetId)
            .AnyAsync(
                s => s.Path.IsWithinDistance(playerLocation, thresholdMeters),
                cancellationToken);

        return isNear;
    }

    /// <inheritdoc />
    public async Task<(int StreetId, string StreetName)?> FindNearestStreetAsync(
        Point playerLocation,
        double thresholdMeters = 15,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playerLocation);

        var nearest = await db.Streets
            .Where(s => s.Path.IsWithinDistance(playerLocation, thresholdMeters))
            .OrderBy(s => s.Path.Distance(playerLocation))
            .Select(s => new { s.Id, s.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (nearest is null) return null;

        return (nearest.Id, nearest.Name);
    }

    /// <inheritdoc />
    public async Task<double> CalculateStreetFractionAsync(
        int streetId,
        Point playerLocation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playerLocation);

        // ST_LineLocatePoint returns a float between 0 and 1 representing
        // the fraction along the LineString closest to the given point.
        var fraction = await db.Database
            .SqlQueryRaw<double>(
                """
                SELECT ST_LineLocatePoint(s."Path", ST_SetSRID(ST_MakePoint({0}, {1}), 4326))
                FROM "Streets" s
                WHERE s."Id" = {2}
                """,
                playerLocation.X, // longitude
                playerLocation.Y, // latitude
                streetId)
            .FirstAsync(cancellationToken);

        return fraction;
    }
}
