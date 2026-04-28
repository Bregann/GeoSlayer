using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Journey.Requests;
using GeoSlayer.Domain.DTOs.Journey.Responses;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace GeoSlayer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JourneyController(
    AppDbContext db,
    ILocationService locationService,
    StreetImportService streetImportService) : ControllerBase
{
    private const int XpPerStreet = 50;

    /// <summary>Radius in metres to scan for nearby POIs.</summary>
    private const double PoiScanRadius = 200;

    /// <summary>Radius in metres at which a POI becomes interactable.</summary>
    private const double PoiInteractRadius = 50;

    /// <summary>
    /// Accepts the player's current position, updates street-walking progress,
    /// and awards XP when a street is fully conquered.
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult<SyncResponse>> Sync(
        [FromBody] SyncRequest request,
        CancellationToken ct)
    {
        var playerLocation = new Point(request.Longitude, request.Latitude) { SRID = 4326 };

        // Ensure the grid cell for this location has street data loaded
        await streetImportService.EnsureCellLoadedAsync(request.Latitude, request.Longitude);

        // Update the player's stored location
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == request.PlayerId, ct);
        if (player is null)
            return NotFound("Player not found");

        player.Location = playerLocation;

        // Find the nearest street
        var nearest = await locationService.FindNearestStreetAsync(playerLocation, thresholdMeters: 15, ct);
        if (nearest is null)
        {
            await db.SaveChangesAsync(ct);
            var pois = await GetNearbyPoisAsync(playerLocation, ct);
            return Ok(new SyncResponse
            {
                StreetName = null,
                PercentComplete = 0,
                JustConquered = false,
                Xp = player.Xp,
                Level = player.Level,
                NearbyPois = pois,
            });
        }

        var (streetId, streetName) = nearest.Value;

        // Get the player's fraction along the street
        var fraction = await locationService.CalculateStreetFractionAsync(streetId, playerLocation, ct);

        // Get or create progress record
        var progress = await db.UserStreetProgresses
            .FirstOrDefaultAsync(p => p.PlayerId == request.PlayerId && p.StreetId == streetId, ct);

        if (progress is null)
        {
            progress = new UserStreetProgress
            {
                PlayerId = request.PlayerId,
                StreetId = streetId,
                CoveredMinFraction = fraction,
                CoveredMaxFraction = fraction,
                PercentComplete = 0,
                IsConquered = false,
            };
            db.UserStreetProgresses.Add(progress);
        }
        else
        {
            // Expand the covered range
            if (fraction < progress.CoveredMinFraction) progress.CoveredMinFraction = fraction;
            if (fraction > progress.CoveredMaxFraction) progress.CoveredMaxFraction = fraction;
        }

        progress.PercentComplete = Math.Min(100, (progress.CoveredMaxFraction - progress.CoveredMinFraction) * 100);

        // Award XP on first completion
        bool justConquered = false;
        if (progress.PercentComplete >= 100 && !progress.IsConquered)
        {
            progress.IsConquered = true;
            justConquered = true;
            player.Xp += XpPerStreet;

            // Level-up check (need level × 100 XP to advance)
            while (player.Xp >= player.Level * 100)
            {
                player.Xp -= player.Level * 100;
                player.Level++;
            }
        }

        await db.SaveChangesAsync(ct);

        var nearbyPois = await GetNearbyPoisAsync(playerLocation, ct);

        return Ok(new SyncResponse
        {
            StreetName = streetName,
            PercentComplete = Math.Round(progress.PercentComplete, 1),
            JustConquered = justConquered,
            Xp = player.Xp,
            Level = player.Level,
            NearbyPois = nearbyPois,
        });
    }

    /// <summary>
    /// Returns POIs within <see cref="PoiScanRadius"/> metres, sorted by distance.
    /// </summary>
    private async Task<List<NearbyPoiDto>> GetNearbyPoisAsync(
        Point playerLocation, CancellationToken ct)
    {
        // Use raw SQL for accurate geography distance in metres
        var pois = await db.PointsOfInterest
            .Where(p => p.Location.IsWithinDistance(playerLocation, PoiScanRadius))
            .OrderBy(p => p.Location.Distance(playerLocation))
            .Take(30)
            .Select(p => new NearbyPoiDto
            {
                Id = p.Id,
                Name = p.Name,
                Skill = p.Skill.ToString(),
                Latitude = p.Location.Y,
                Longitude = p.Location.X,
                XpReward = p.XpReward,
                DistanceMetres = p.Location.Distance(playerLocation),
                InRange = p.Location.IsWithinDistance(playerLocation, PoiInteractRadius),
            })
            .ToListAsync(ct);

        return pois;
    }
}
