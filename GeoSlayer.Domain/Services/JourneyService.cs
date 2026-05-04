using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Journey.Requests;
using GeoSlayer.Domain.DTOs.Journey.Responses;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Services;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace GeoSlayer.Domain.Services;

public class JourneyService(
    AppDbContext db,
    IFogService fogService,
    IPoiImportService poiImportService) : IJourneyService
{
    private const double PoiCellSize = 0.05;
    private const double PoiScanRadius = 200;
    private const double PoiInteractRadius = 50;

    // ── Sync ───────────────────────────────────────────────────────

    public async Task<SyncResponse> Sync(SyncRequest request, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == request.PlayerId, ct)
            ?? throw new NotFoundException("Player not found");

        // Ensure POIs are imported for this area
        await EnsurePoisImported(request.Latitude, request.Longitude, ct);

        // Update coarse grid cell for preloading
        var cellLat = SnapToPoiGrid(request.Latitude);
        var cellLng = SnapToPoiGrid(request.Longitude);
        player.LastCellLat = cellLat;
        player.LastCellLng = cellLng;

        // Reveal fog-of-war cells (includes anti-cheat validation)
        var fogResult = await fogService.Reveal(
            request.PlayerId, request.Latitude, request.Longitude, request.TimestampMs, ct);

        await db.SaveChangesAsync(ct);

        // Load nearby POIs
        var playerLocation = new Point(request.Longitude, request.Latitude) { SRID = 4326 };
        var nearbyPois = await GetNearbyPois(playerLocation, ct);

        // Reload player after potential XP changes
        await db.Entry(player).ReloadAsync(ct);

        return new SyncResponse
        {
            NewCells = fogResult.NewCells,
            Xp = player.Xp,
            Level = player.Level,
            NearbyPois = nearbyPois,
        };
    }

    // ── Revealed cells ─────────────────────────────────────────────

    public async Task<List<CellDto>> GetRevealedCells(int playerId, CancellationToken ct)
    {
        var exists = await db.Players.AnyAsync(p => p.Id == playerId, ct);
        if (!exists)
            throw new NotFoundException("Player not found");

        return await fogService.GetAllRevealed(playerId, ct);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private async Task EnsurePoisImported(double latitude, double longitude, CancellationToken ct)
    {
        var cellLat = SnapToPoiGrid(latitude);
        var cellLng = SnapToPoiGrid(longitude);

        var alreadyImported = await db.ImportedRegions
            .AnyAsync(r => r.CellLat == cellLat && r.CellLng == cellLng, ct);

        if (!alreadyImported)
        {
            await poiImportService.ImportCellPois(cellLat, cellLng);
            db.ImportedRegions.Add(new ImportedRegion
            {
                CellLat = cellLat,
                CellLng = cellLng,
                ImportedAtUtc = DateTime.UtcNow,
            });
        }
    }

    private async Task<List<NearbyPoiDto>> GetNearbyPois(Point playerLocation, CancellationToken ct)
    {
        return await db.PointsOfInterest
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
    }

    private static double SnapToPoiGrid(double value) =>
        Math.Floor(value / PoiCellSize) * PoiCellSize;
}
