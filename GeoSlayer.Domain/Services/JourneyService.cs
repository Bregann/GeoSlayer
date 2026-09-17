using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Journey.Requests;
using GeoSlayer.Domain.DTOs.Journey.Responses;
using GeoSlayer.Domain.DTOs.Idle.Responses;
using GeoSlayer.Domain.DTOs.Skills.Responses;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Interfaces.Helpers;
using GeoSlayer.Domain.Services;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace GeoSlayer.Domain.Services;

public class JourneyService(
    AppDbContext db,
    IFogService fogService,
    IPoiImportService poiImportService,
    ISkillTrainingService skillTraining,
    IWorkerService workerService,
    ICraftingService craftingService,
    IRetentionService retentionService,
    IUserContextHelper userContextHelper) : IJourneyService
{
    private const double PoiCellSize = 0.05;
    private const double PoiScanRadius = 200;
    private const double PoiInteractRadius = 50;

    // ── Sync ───────────────────────────────────────────────────────

    public async Task<SyncResponse> Sync(SyncRequest request, CancellationToken ct)
    {
        var player = await CurrentPlayer(ct);

        var path = request.Path();

        if (path.Count == 0)
            throw new BadRequestException("Sync requires at least one position");

        var last = path[^1];

        // Ensure POIs are imported for this area — around where the player ended up
        await EnsurePoisImported(last.Latitude, last.Longitude, ct);

        // Update coarse grid cell for preloading
        var cellLat = SnapToPoiGrid(last.Latitude);
        var cellLng = SnapToPoiGrid(last.Longitude);
        player.LastCellLat = cellLat;
        player.LastCellLng = cellLng;

        // Collect what workers produced while away, before the walk is processed, so the
        // welcome-back figures describe the absence rather than including this sync (§5.3).
        var offlineAccrual = await workerService.CollectOfflineAccrual(player.Id, ct);

        // Crafts finish lazily too (§4.2 / §5.3) — no recurring job for either.
        var craftCollection = await craftingService.CollectCompletedCrafts(player.Id, ct);

        // Expeditions return lazily too — the same principle as workers and crafts.
        var expeditions = await retentionService.CollectExpeditions(player.Id, ct);

        // Reveal fog-of-war cells (includes anti-cheat validation)
        var fogResult = await fogService.Reveal(player.Id, path, ct);

        await db.SaveChangesAsync(ct);

        // A completed patrol circuit pays upkeep (§5.7). Checked against this batch's
        // path, so the loop is detected as it is walked.
        var patrols = await retentionService.CheckPatrolCompletion(
            player.Id,
            path.Select(p => (p.Latitude, p.Longitude)).ToList(),
            ct);

        // Surges are a property of a place, generated on demand for the region the player
        // is actually in — cheap, and it means nobody sees an empty map.
        await retentionService.EnsureSurgeFor(last.Latitude, last.Longitude, ct);

        var surges = await retentionService.GetActiveSurges(last.Latitude, last.Longitude, ct);

        // Load nearby POIs
        var playerLocation = new Point(last.Longitude, last.Latitude) { SRID = 4326 };
        var nearbyPois = await GetNearbyPois(playerLocation, ct);

        // Reload player after potential XP changes
        await db.Entry(player).ReloadAsync(ct);

        return new SyncResponse
        {
            NewCells = fogResult.NewCells,
            Xp = player.AdventurerXp,
            Level = player.AdventurerLevel,
            Unlocks = fogResult.Grant?.Unlocks ?? [],
            Materials = fogResult.Materials,
            SkillTraining = fogResult.SkillTraining,
            MuseumAcquisitions = fogResult.MuseumAcquisitions,
            OfflineAccrual = offlineAccrual.HasAccrual ? offlineAccrual : null,
            CraftCollection = craftCollection.HasCollection ? craftCollection : null,
            ExpeditionCollection = expeditions.HasCollection ? expeditions : null,
            PatrolCompletions = patrols,
            TransitBanked = fogResult.TransitBanked,
            TransitRedemption = fogResult.TransitRedemption,
            Surges = surges,
            BonusPointsGranted = fogResult.Grant?.BonusPointsGranted ?? 0,
            NearbyPois = nearbyPois,
        };
    }

    // ── Revealed cells ─────────────────────────────────────────────

    public async Task<List<CellDto>> GetRevealedCells(CancellationToken ct)
    {
        var player = await CurrentPlayer(ct);
        return await fogService.GetAllRevealed(player.Id, ct);
    }

    // ── POI visits ─────────────────────────────────────────────────

    public async Task<PoiVisitResultDto> VisitPoi(int poiId, CancellationToken ct)
    {
        // The player id comes from the JWT, never the request, so one account cannot
        // visit on another's behalf.
        var player = await CurrentPlayer(ct);
        return await skillTraining.VisitPoi(player.Id, poiId, ct);
    }

    // ── Helpers ────────────────────────────────────────────────────

    /// <summary>
    /// The player belonging to the authenticated caller.  The client never supplies a
    /// player id — it is derived from the JWT so one account cannot touch another's map.
    /// </summary>
    private async Task<Player> CurrentPlayer(CancellationToken ct)
    {
        var userId = userContextHelper.GetUserId();

        return await db.Players.FirstOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new NotFoundException("Player not found");
    }

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
