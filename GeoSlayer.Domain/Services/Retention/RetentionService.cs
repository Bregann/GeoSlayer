using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Retention.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Services.Fog;
using GeoSlayer.Domain.Services.Idle;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Services.Retention;

/// <summary>
/// The retention systems (Stage 14, DESIGN.md §5.4–5.7, §7.1).
/// </summary>
public class RetentionService(
    AppDbContext db,
    IMaterialService materials,
    IProgressionService progression) : IRetentionService
{
    // ── Uncharted Transit (§7.1) ────────────────────────────────────

    public async Task<int> BankTransit(
        int playerId, IReadOnlyList<GridCell> cells, double speedMetresPerSecond, CancellationToken ct)
    {
        var weight = TransitGrading.TransitWeight(speedMetresPerSecond);

        // At walking pace nothing banks — the cells simply revealed.
        if (weight <= 0 || cells.Count == 0) return 0;

        var since = DateTime.UtcNow.Date;

        var bankedToday = await db.BankedTransits
            .CountAsync(t => t.PlayerId == playerId && t.BankedUtc >= since, ct);

        // Capped per day so a long-haul flight does not bank a continent (§7.1).
        var room = TransitGrading.DailyTransitCap - bankedToday;
        if (room <= 0) return 0;

        var existing = (await db.BankedTransits
                .Where(t => t.PlayerId == playerId && !t.Redeemed)
                .Select(t => new { t.GridLat, t.GridLng })
                .ToListAsync(ct))
            .Select(t => (t.GridLat, t.GridLng))
            .ToHashSet();

        // Already-revealed ground is not "somewhere you passed but did not see".
        var revealed = (await db.RevealedCells
                .Where(r => r.PlayerId == playerId)
                .Select(r => new { r.GridLat, r.GridLng })
                .ToListAsync(ct))
            .Select(r => (r.GridLat, r.GridLng))
            .ToHashSet();

        var now = DateTime.UtcNow;
        var banked = 0;

        foreach (var cell in cells)
        {
            if (banked >= room) break;

            var key = (cell.GridLat, cell.GridLng);

            if (revealed.Contains(key)) continue;
            if (!existing.Add(key)) continue;

            db.BankedTransits.Add(new BankedTransit
            {
                PlayerId = playerId,
                GridLat = cell.GridLat,
                GridLng = cell.GridLng,
                BankedUtc = now,
                Weight = weight,
            });

            banked++;
        }

        if (banked > 0) await db.SaveChangesAsync(ct);

        return banked;
    }

    public async Task<TransitRedemptionDto> RedeemTransit(
        int playerId, IReadOnlyList<GridCell> walkedCells, CancellationToken ct)
    {
        var result = new TransitRedemptionDto();

        if (walkedCells.Count == 0) return result;

        var cutoff = DateTime.UtcNow - TransitGrading.DecayWindow;

        // Expired transit is swept here rather than by a job — same lazy principle as
        // worker accrual, and it costs nothing on a request that was already loading
        // these rows.
        var expired = await db.BankedTransits
            .Where(t => t.PlayerId == playerId && !t.Redeemed && t.BankedUtc < cutoff)
            .ToListAsync(ct);

        if (expired.Count > 0)
        {
            db.BankedTransits.RemoveRange(expired);
            result.Expired = expired.Count;
        }

        var banked = await db.BankedTransits
            .Where(t => t.PlayerId == playerId && !t.Redeemed && t.BankedUtc >= cutoff)
            .ToListAsync(ct);

        if (banked.Count == 0)
        {
            if (expired.Count > 0) await db.SaveChangesAsync(ct);
            return result;
        }

        // Each walked cell redeems a few banked ones nearby — which is what makes a
        // commute "a reason to walk, seeded along routes they already travel".
        var budget = walkedCells.Count * TransitGrading.RedeemedPerWalkedCell;

        var radiusCells = (int)Math.Ceiling(
            TransitGrading.RedemptionRadiusMetres / (FogService.CellSize * 111_320.0));

        var walked = walkedCells.Select(c => (c.GridLat, c.GridLng)).ToHashSet();

        var redeemed = new List<BankedTransit>();

        foreach (var transit in banked.OrderBy(t => t.BankedUtc))
        {
            if (redeemed.Count >= budget) break;

            var near = walked.Any(w =>
                Math.Abs(w.GridLat - transit.GridLat) <= radiusCells
                && Math.Abs(w.GridLng - transit.GridLng) <= radiusCells);

            if (near) redeemed.Add(transit);
        }

        var now = DateTime.UtcNow;

        var alreadyRevealed = (await db.RevealedCells
                .Where(r => r.PlayerId == playerId)
                .Select(r => new { r.GridLat, r.GridLng })
                .ToListAsync(ct))
            .Select(r => (r.GridLat, r.GridLng))
            .ToHashSet();

        foreach (var transit in redeemed)
        {
            transit.Redeemed = true;

            if (!alreadyRevealed.Add((transit.GridLat, transit.GridLng))) continue;

            db.RevealedCells.Add(new RevealedCell
            {
                PlayerId = playerId,
                GridLat = transit.GridLat,
                GridLng = transit.GridLng,
                RevealedAtUtc = now,
            });

            result.CellsRevealed++;
        }

        await db.SaveChangesAsync(ct);

        result.Redeemed = redeemed.Count;

        result.Remaining = await db.BankedTransits
            .CountAsync(t => t.PlayerId == playerId && !t.Redeemed, ct);

        return result;
    }

    public async Task<List<BankedTransitDto>> GetBankedTransit(int playerId, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow - TransitGrading.DecayWindow;

        var banked = await db.BankedTransits
            .Where(t => t.PlayerId == playerId && !t.Redeemed && t.BankedUtc >= cutoff)
            .ToListAsync(ct);

        return banked.Select(t =>
        {
            var (south, west, north, east) = FogService.CellBounds(t.GridLat, t.GridLng);

            return new BankedTransitDto
            {
                GridLat = t.GridLat,
                GridLng = t.GridLng,
                South = south,
                West = west,
                North = north,
                East = east,
                Weight = t.Weight,
                ExpiresUtc = t.BankedUtc + TransitGrading.DecayWindow,
            };
        }).ToList();
    }

    // ── Expeditions (§5.4) ──────────────────────────────────────────

    public async Task<ExpeditionDto> DispatchExpedition(
        int playerId, int workerId, int poiId, CancellationToken ct)
    {
        var worker = await db.Workers
            .FirstOrDefaultAsync(w => w.Id == workerId && w.PlayerId == playerId, ct)
            ?? throw new NotFoundException($"Worker {workerId} not found.");

        // Only POIs the player has personally visited (§5.4). The Stage 04 visit log is
        // exactly this, which is what makes the feature nearly free.
        var visit = await db.PlayerPoiVisits
            .FirstOrDefaultAsync(v => v.PlayerId == playerId && v.PoiId == poiId, ct)
            ?? throw new BadRequestException("You have never been there. Visit it first.");

        var poi = await db.PointsOfInterest.FirstOrDefaultAsync(p => p.Id == poiId, ct)
            ?? throw new NotFoundException($"POI {poiId} not found.");

        var active = await db.WorkerExpeditions
            .AnyAsync(e => e.WorkerId == workerId && !e.Collected, ct);

        if (active) throw new BadRequestException("That worker is already away.");

        var player = await db.Players.FirstAsync(p => p.Id == playerId, ct);

        var distance = TraceValidator.HaversineMetres(
            player.LastLatitude, player.LastLongitude, poi.Location.Y, poi.Location.X);

        var now = DateTime.UtcNow;

        var expedition = new WorkerExpedition
        {
            PlayerId = playerId,
            WorkerId = workerId,
            PoiId = poiId,
            PoiName = poi.Name,
            DispatchedUtc = now,
            ReturnsUtc = now + ExpeditionMath.Duration(distance),
            DistanceMetres = distance,
        };

        db.WorkerExpeditions.Add(expedition);

        // Occupies the worker, competing with Claim work — a real decision (§5.4).
        worker.ClaimId = null;

        await db.SaveChangesAsync(ct);

        return ToDto(expedition, now);
    }

    public async Task<List<ExpeditionDto>> GetExpeditions(int playerId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var expeditions = await db.WorkerExpeditions
            .Where(e => e.PlayerId == playerId && !e.Collected)
            .OrderBy(e => e.ReturnsUtc)
            .ToListAsync(ct);

        return expeditions.Select(e => ToDto(e, now)).ToList();
    }

    private static ExpeditionDto ToDto(WorkerExpedition expedition, DateTime now) => new()
    {
        Id = expedition.Id,
        WorkerId = expedition.WorkerId,
        PoiId = expedition.PoiId,
        PoiName = expedition.PoiName,
        DispatchedUtc = expedition.DispatchedUtc,
        ReturnsUtc = expedition.ReturnsUtc,
        DistanceMetres = expedition.DistanceMetres,
        HasReturned = expedition.ReturnsUtc <= now,
        SecondsRemaining = Math.Max(0, (expedition.ReturnsUtc - now).TotalSeconds),
    };

    public async Task<List<ExpeditionDestinationDto>> GetExpeditionDestinations(
        int playerId, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null) return [];

        // The Stage 04 visit log *is* the destination list. Nothing else is needed, which
        // is why §5.4 calls this "nearly free to implement".
        var visits = await db.PlayerPoiVisits
            .Include(v => v.Poi)
            .Where(v => v.PlayerId == playerId)
            .ToListAsync(ct);

        if (visits.Count == 0) return [];

        var busyPoiIds = (await db.WorkerExpeditions
                .Where(e => e.PlayerId == playerId && !e.Collected)
                .Select(e => e.PoiId)
                .ToListAsync(ct))
            .ToHashSet();

        var destinations = visits
            .Where(v => v.Poi is not null)
            .Select(v =>
            {
                var distance = TraceValidator.HaversineMetres(
                    player.LastLatitude, player.LastLongitude,
                    v.Poi.Location.Y, v.Poi.Location.X);

                return new ExpeditionDestinationDto
                {
                    PoiId = v.PoiId,
                    Name = v.Poi.Name,
                    Skill = v.Poi.Skill,
                    SkillName = v.Poi.Skill.ToString(),
                    FirstVisitUtc = v.FirstVisitUtc,
                    TotalVisits = v.TotalVisits,
                    DistanceMetres = distance,
                    DurationHours = ExpeditionMath.Duration(distance).TotalHours,

                    // Shown before dispatching so the distance/time trade-off is a real
                    // decision rather than a guess.
                    EstimatedMaterials = ExpeditionMath.MaterialYield(distance, 1),
                    IsAvailable = !busyPoiIds.Contains(v.PoiId),
                };
            })
            // Furthest first: a distant POI is the interesting choice, and the whole
            // point is that an old holiday trip still pays.
            .OrderByDescending(d => d.DistanceMetres)
            .ToList();

        return destinations;
    }

    public async Task<ExpeditionCollectionDto> CollectExpeditions(int playerId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Lazy, like everything else in the idle half — no job walks these.
        var returned = await db.WorkerExpeditions
            .Include(e => e.Worker)
            .Where(e => e.PlayerId == playerId && !e.Collected && e.ReturnsUtc <= now)
            .ToListAsync(ct);

        var result = new ExpeditionCollectionDto();

        if (returned.Count == 0) return result;

        var totals = new Dictionary<int, int>();

        foreach (var expedition in returned)
        {
            expedition.Collected = true;
            result.Returned.Add(expedition.PoiName);

            var poi = await db.PointsOfInterest.FirstOrDefaultAsync(p => p.Id == expedition.PoiId, ct);
            if (poi is null) continue;

            var tier = expedition.Worker?.Tier ?? 1;

            // The POI's own skill decides what comes back — which is what gives a rural
            // player lasting access to urban materials after one city trip (§5.4).
            var material = await db.Materials
                .Where(m => m.SkillType == poi.Skill && !m.IsUnique)
                .OrderByDescending(m => m.Tier)
                .FirstOrDefaultAsync(ct);

            if (material is not null)
            {
                var units = ExpeditionMath.MaterialYield(expedition.DistanceMetres, tier);
                totals[material.Id] = totals.GetValueOrDefault(material.Id) + units;
            }

            var unlocked = await db.PlayerSkills
                .AnyAsync(s => s.PlayerId == playerId && s.SkillType == poi.Skill, ct);

            if (unlocked)
            {
                var xp = ExpeditionMath.XpYield(expedition.DistanceMetres, tier);

                // XpSource.Idle: an expedition is the worker's time, so it pays the
                // reduced Adventurer ratio like any other idle source.
                var grant = await progression.GrantXp(playerId, poi.Skill, xp, XpSource.Idle, ct);

                result.SkillXpEarned += grant.SkillXpEarned;
            }
        }

        await db.SaveChangesAsync(ct);

        if (totals.Count > 0)
            result.Materials = await materials.GrantMaterials(playerId, totals, ct);

        result.HasCollection = true;

        return result;
    }

    // ── Patrol routes (§5.7) ────────────────────────────────────────

    public async Task<PatrolRouteDto> CreatePatrolRoute(
        int playerId, string name, IReadOnlyList<(double Lat, double Lng)> waypoints, CancellationToken ct)
    {
        if (waypoints.Count < 2)
            throw new BadRequestException("A patrol needs at least two waypoints.");

        var route = new PatrolRoute
        {
            PlayerId = playerId,
            Name = string.IsNullOrWhiteSpace(name) ? "Patrol" : name.Trim(),
            CreatedUtc = DateTime.UtcNow,
        };

        var sequence = 0;

        foreach (var (lat, lng) in waypoints)
        {
            route.Waypoints.Add(new PatrolWaypoint
            {
                Sequence = sequence++,
                Latitude = lat,
                Longitude = lng,
            });
        }

        db.PatrolRoutes.Add(route);
        await db.SaveChangesAsync(ct);

        return ToDto(route);
    }

    public async Task<List<PatrolRouteDto>> GetPatrolRoutes(int playerId, CancellationToken ct)
    {
        var routes = await db.PatrolRoutes
            .Include(r => r.Waypoints)
            .Where(r => r.PlayerId == playerId)
            .ToListAsync(ct);

        return routes.Select(ToDto).ToList();
    }

    private static PatrolRouteDto ToDto(PatrolRoute route) => new()
    {
        Id = route.Id,
        Name = route.Name,
        WaypointCount = route.Waypoints.Count,
        CompletionCount = route.CompletionCount,
        LastCompletedUtc = route.LastCompletedUtc,
        UpkeepReward = PatrolMatching.UpkeepReward(route.Waypoints.Count),
        Waypoints = route.Waypoints
            .OrderBy(w => w.Sequence)
            .Select(w => new PatrolWaypointDto { Latitude = w.Latitude, Longitude = w.Longitude })
            .ToList(),
    };

    public async Task<List<PatrolCompletionDto>> CheckPatrolCompletion(
        int playerId, IReadOnlyList<(double Lat, double Lng)> path, CancellationToken ct)
    {
        if (path.Count == 0) return [];

        var routes = await db.PatrolRoutes
            .Include(r => r.Waypoints)
            .Where(r => r.PlayerId == playerId)
            .ToListAsync(ct);

        if (routes.Count == 0) return [];

        var fixes = path.Select(p => new PatrolMatching.Fix(p.Lat, p.Lng)).ToList();

        var completions = new List<PatrolCompletionDto>();
        var now = DateTime.UtcNow;

        foreach (var route in routes)
        {
            // One completion per day, so a loop walked twice does not pay twice — the
            // reward is maintenance, not a grind target.
            if (route.LastCompletedUtc is not null && (now - route.LastCompletedUtc.Value).TotalHours < 20)
                continue;

            var waypoints = route.Waypoints
                .OrderBy(w => w.Sequence)
                .Select(w => new PatrolMatching.Fix(w.Latitude, w.Longitude))
                .ToList();

            if (!PatrolMatching.CompletesCircuit(fixes, waypoints)) continue;

            route.CompletionCount += 1;
            route.LastCompletedUtc = now;

            var food = PatrolMatching.UpkeepReward(waypoints.Count);

            // Upkeep, deliberately — §5.7 keeps novelty as the only route to progress and
            // makes routine the route to maintenance. No cell XP: re-walking is not new
            // ground, and the reveal path already refuses a duplicate cell.
            var rations = await db.Materials.FirstOrDefaultAsync(m => m.Key == "dried_rations", ct);

            var gains = rations is null
                ? []
                : await materials.GrantMaterials(
                    playerId, new Dictionary<int, int> { [rations.Id] = food }, ct);

            completions.Add(new PatrolCompletionDto
            {
                RouteId = route.Id,
                Name = route.Name,
                UpkeepAwarded = food,
                Materials = gains,
            });
        }

        if (completions.Count > 0) await db.SaveChangesAsync(ct);

        return completions;
    }

    // ── Districts (§5.5) ────────────────────────────────────────────

    public async Task<DistrictStatusDto> GetDistrictStatus(int playerId, CancellationToken ct)
    {
        var claims = await db.Claims
            .Where(c => c.PlayerId == playerId)
            .Select(c => new { c.Id, c.CentreGridLat, c.CentreGridLng, c.TerrainProfile })
            .ToListAsync(ct);

        var definitions = await db.DistrictDefinitions.ToListAsync(ct);

        var nodes = claims
            .Select(c => new DistrictMatching.ClaimNode(
                c.Id, c.CentreGridLat, c.CentreGridLng, c.TerrainProfile))
            .ToList();

        var (definition, bonus) = DistrictMatching.BestMatch(nodes, definitions);

        var status = new DistrictStatusDto
        {
            DistrictKey = definition?.Key,
            Name = definition?.Name,
            Description = definition?.Description,
            OutputBonus = bonus,
            ClaimCount = nodes.Count,
        };

        // Only when they have none: with a District formed, the shortfall to some other
        // one is noise. Without it, "no District" gives the player nothing to act on.
        if (definition is null)
        {
            var nearest = DistrictMatching.NearestMiss(nodes, definitions);

            if (nearest is not null)
            {
                status.NearestName = nearest.Value.Definition.Name;
                status.MissingTerrains = nearest.Value.MissingTerrains
                    .Select(t => t.ToString()).ToList();
                status.MissingClaims = nearest.Value.MissingClaims;
            }
        }

        return status;
    }

    // ── Surges (§5.6) ───────────────────────────────────────────────

    public async Task<List<SurgeDto>> GetActiveSurges(
        double latitude, double longitude, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var cellLat = Museum.PoiRegionResolver.SnapToGrid(latitude);
        var cellLng = Museum.PoiRegionResolver.SnapToGrid(longitude);

        var surges = await db.ResourceSurges
            .Where(s => s.StartsUtc <= now && s.EndsUtc > now
                     && s.CellLat == cellLat && s.CellLng == cellLng)
            .ToListAsync(ct);

        return surges.Select(s => new SurgeDto
        {
            Id = s.Id,
            Description = s.Description,
            TargetTerrain = s.TargetTerrain,
            TargetSkill = s.TargetSkill,
            Multiplier = s.Multiplier,
            EndsUtc = s.EndsUtc,
        }).ToList();
    }

    public async Task<SurgeDto?> EnsureSurgeFor(
        double latitude, double longitude, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var cellLat = Museum.PoiRegionResolver.SnapToGrid(latitude);
        var cellLng = Museum.PoiRegionResolver.SnapToGrid(longitude);

        var active = await db.ResourceSurges
            .AnyAsync(s => s.CellLat == cellLat && s.CellLng == cellLng
                        && s.StartsUtc <= now && s.EndsUtc > now, ct);

        if (active) return null;

        // Generated server-side against existing regions — cheap to run, and deterministic
        // on the cell and day so two players in the same place see the same surge.
        var seed = HashCode.Combine(cellLat, cellLng, now.Date.DayOfYear);
        var random = new Random(seed);

        var terrains = Enum.GetValues<TerrainType>().Where(t => t != TerrainType.Open).ToList();
        var terrain = terrains[random.Next(terrains.Count)];

        var surge = new ResourceSurge
        {
            CellLat = cellLat,
            CellLng = cellLng,
            TargetTerrain = terrain,
            // Modest by design: "a player who ignores every surge must still progress
            // fine" (§5.6). A nudge, not an obligation.
            Multiplier = 1.5,
            Description = $"{terrain} ground is yielding well here today.",
            StartsUtc = now,
            EndsUtc = now.AddHours(12),
        };

        db.ResourceSurges.Add(surge);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return null;
        }

        return new SurgeDto
        {
            Id = surge.Id,
            Description = surge.Description,
            TargetTerrain = surge.TargetTerrain,
            TargetSkill = surge.TargetSkill,
            Multiplier = surge.Multiplier,
            EndsUtc = surge.EndsUtc,
        };
    }
}
