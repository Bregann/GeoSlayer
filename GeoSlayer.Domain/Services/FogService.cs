using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Journey.Requests;
using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Skills.Responses;
using GeoSlayer.Domain.DTOs.Progression.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Services.Fog;
using GeoSlayer.Domain.Services.Progression;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Services;

public class FogService(
    AppDbContext db,
    IProgressionService progression,
    IMaterialService materials,
    ISkillTrainingService skillTraining,
    ICraftingService crafting) : IFogService
{
    /// <summary>
    /// Grid cell size in degrees.  0.0009° ≈ 100 m at the equator, ~64 m at 45° latitude.
    /// </summary>
    public const double CellSize = 0.0009;

    /// <summary>
    /// Base cells revealed around each swept cell (1 = a 3×3 block).  The Reveal Radius
    /// upgrade adds to this (§3.0a), so the effective radius is per-player.
    /// </summary>
    private const int BaseRevealRadius = 1;

    // ── Anti-cheat constants ───────────────────────────────────────

    /// <summary>Max plausible travel speed (m/s).  50 m/s ≈ 180 km/h — faster than any road vehicle.</summary>
    private const double MaxSpeedMetresPerSecond = 50.0;

    /// <summary>Minimum interval between syncs from the same player.</summary>
    private const double MinSyncIntervalSeconds = 2.0;

    /// <summary>
    /// Max cells revealable in a single sync.  A swept path legitimately covers hundreds
    /// of cells over a long background batch, so this is a sanity backstop against a
    /// forged multi-kilometre path, not a per-walk budget.
    /// </summary>
    private const int MaxNewCellsPerSync = 2_000;

    /// <summary>Max allowable clock skew for the client timestamp (seconds).</summary>
    private const double MaxClientClockSkewSeconds = 300;

    // ── Grid helpers ────────────────────────────────────────────────

    public static int ToGrid(double value) => (int)Math.Floor(value / CellSize);

    public static (double south, double west, double north, double east) CellBounds(int gridLat, int gridLng)
    {
        var south = gridLat * CellSize;
        var west = gridLng * CellSize;
        return (south, west, south + CellSize, west + CellSize);
    }

    // ── Reveal ──────────────────────────────────────────────────────

    /// <summary>
    /// Reveal every fog cell swept by the player's path, oldest position first.
    /// Includes anti-cheat checks: speed cap, sync cooldown, client-clock sanity.
    /// Returns an empty result when the sync is rejected.
    /// </summary>
    public async Task<FogRevealResult> Reveal(
        int playerId, IReadOnlyList<SyncPosition> path, CancellationToken ct)
    {
        if (path.Count == 0)
            return FogRevealResult.Empty;

        var now = DateTime.UtcNow;
        var player = await db.Players.FirstAsync(p => p.Id == playerId, ct);

        var last = path[^1];

        // ── Anti-cheat: client timestamp sanity ───────────────────
        // Judge the batch by its newest fix; earlier ones are legitimately older.
        var clientTime = DateTimeOffset.FromUnixTimeMilliseconds(last.TimestampMs).UtcDateTime;
        if (Math.Abs((now - clientTime).TotalSeconds) > MaxClientClockSkewSeconds)
            return FogRevealResult.Empty;

        // ── Anti-cheat: sync cooldown ─────────────────────────────
        // Rate-limits how often a client may *call* us.  It deliberately says nothing
        // about how long the batch covers: a 40-minute batch arriving 1s after the last
        // sync is spam, while the same batch 10s later is a normal background flush.
        if (player.LastSyncAtUtc.HasValue)
        {
            var secondsSinceLastSync = (now - player.LastSyncAtUtc.Value).TotalSeconds;
            if (secondsSinceLastSync < MinSyncIntervalSeconds)
                return FogRevealResult.Empty;

            // ── Anti-cheat: speed / distance cap ─────────────────
            if (player.LastLatitude != 0 || player.LastLongitude != 0)
            {
                var distance = TraceValidator.HaversineMetres(
                    player.LastLatitude, player.LastLongitude, last.Latitude, last.Longitude);
                var maxAllowed = MaxSpeedMetresPerSecond * secondsSinceLastSync;

                // Allow a 100 m grace buffer for GPS drift
                if (distance > maxAllowed + 100)
                    return FogRevealResult.Empty;
            }
        }

        // ── Anti-cheat: cross-sync dwell ──────────────────────────
        // A batch alone cannot see a phone that has sat still for an hour syncing every
        // 30 seconds: each batch is individually too short to look like dwelling. Compare
        // against where the player was at the previous sync to catch it.
        var dwelling = player.LastSyncAtUtc.HasValue
            && (player.LastLatitude != 0 || player.LastLongitude != 0)
            && (now - player.LastSyncAtUtc.Value).TotalSeconds >= TraceValidator.DwellSeconds
            && TraceValidator.HaversineMetres(
                   player.LastLatitude, player.LastLongitude, last.Latitude, last.Longitude)
               < TraceValidator.DwellRadiusMetres;

        // ── Update player tracking ────────────────────────────────
        // Done before the anti-cheat verdict: a rejected batch still tells us where the
        // player is, and not recording it would let a cheat reset the speed check by
        // alternating good and bad syncs.
        player.LastLatitude = last.Latitude;
        player.LastLongitude = last.Longitude;
        player.LastSyncAtUtc = now;

        if (dwelling)
            return FogRevealResult.Empty;

        // ── Anti-cheat: is this batch real travel? ────────────────
        // Accuracy cutoff, drift, dwell and speed grading.  Swept reveal makes each of
        // these exploits far more valuable, so they gate the sweep, not the other way round.
        var verdict = TraceValidator.Validate(path);
        if (!verdict.Allowed)
            return FogRevealResult.Empty;

        // ── Sweep the path ────────────────────────────────────────
        // Every cell the walked line crosses, not just the cells we happened to get a
        // fix in — otherwise a 40-minute walk between two syncs loses everything between.
        var gridPath = verdict.Accepted
            .Select(p => new GridCell(ToGrid(p.Latitude), ToGrid(p.Longitude)))
            .ToList();

        var swept = PathSweep.SweepPath(gridPath);

        // Reveal Radius is bought with Bonus Points and must change the actual reveal —
        // an upgrade that only displays is worse than no upgrade (§3.0a).
        var bonusRadius = await progression.GetUpgradeEffect(
            playerId, ProgressionDefaults.UpgradeKeys.RevealRadius, ct);

        // Equipped gear stacks on the upgrade (§4.3): the two are different acquisition
        // routes to the same stat, and an equipped item that changes no behaviour is a bug.
        var gearRadius = await crafting.GetModifierTotal(
            playerId, ItemModifier.RevealRadius, ct);

        var candidates = PathSweep.Dilate(
            swept, BaseRevealRadius + (int)bonusRadius + (int)gearRadius);

        var candidateLats = candidates.Select(c => c.GridLat).Distinct().ToList();
        var candidateLngs = candidates.Select(c => c.GridLng).Distinct().ToList();

        // Over-fetches the bounding box of the path rather than the path itself; the
        // set intersection below is what actually decides, and this keeps it to one query.
        var existing = await db.RevealedCells
            .Where(r => r.PlayerId == playerId
                && candidateLats.Contains(r.GridLat)
                && candidateLngs.Contains(r.GridLng))
            .Select(r => new { r.GridLat, r.GridLng })
            .ToListAsync(ct);

        var existingSet = existing
            .Select(e => new GridCell(e.GridLat, e.GridLng))
            .ToHashSet();

        var newCells = new List<CellDto>();
        var inserts = new List<RevealedCell>();

        foreach (var cell in candidates)
        {
            if (existingSet.Contains(cell)) continue;
            if (newCells.Count >= MaxNewCellsPerSync) break;

            inserts.Add(new RevealedCell
            {
                PlayerId = playerId,
                GridLat = cell.GridLat,
                GridLng = cell.GridLng,
                RevealedAtUtc = now,
            });

            var bounds = CellBounds(cell.GridLat, cell.GridLng);
            newCells.Add(new CellDto
            {
                GridLat = cell.GridLat,
                GridLng = cell.GridLng,
                South = bounds.south,
                West = bounds.west,
                North = bounds.north,
                East = bounds.east,
            });
        }

        // One batch insert — a long walk is hundreds of cells, not nine.
        if (inserts.Count > 0)
            db.RevealedCells.AddRange(inserts);

        XpGrantResult? grant = null;
        var materialGains = new List<MaterialGainDto>();
        var skillTraining_ = new List<SkillTrainingDto>();

        if (newCells.Count > 0)
        {
            // Persist the cells before granting: GrantXp saves, and the reveal and its XP
            // must land together or a crash between them pays for cells twice.
            await db.SaveChangesAsync(ct);

            // Every skill whose terrain mapping matches trains from these cells (Stage 04).
            //
            // Exploration used to be granted here directly, as `newCells.Count * 2`. That
            // was a hardcoded per-skill branch, and once Exploration gained a seeded
            // terrain mapping it also double-paid. It is now just another row in
            // SkillTerrainMappings, which is what criterion 13 requires.
            skillTraining_ = await skillTraining.TrainFromCells(
                playerId,
                newCells.Select(c => new GridCell(c.GridLat, c.GridLng)).ToList(),
                ct);

            // Each new cell is also a small flat Adventurer milestone (§3.0b).
            var milestone = await progression.GrantMilestone(
                playerId, MilestoneType.NewCell, newCells.Count, ct);

            // Summed across every skill rather than singling one out. Naming a skill here
            // would be exactly the per-skill branch criterion 13 forbids, and the caller
            // has the full per-skill breakdown in SkillTraining anyway.
            grant = new XpGrantResult
            {
                SkillXpEarned = skillTraining_.Sum(t => t.SkillXpEarned),
                AdventurerXpEarned = skillTraining_.Sum(t => t.AdventurerXpEarned),
                SkillLevelledUp = skillTraining_.Any(t => t.LevelledUp),
            };

            // Materials for the cells just revealed (Stage 03). Rolled per (player, cell)
            // so a replayed sync cannot re-roll for a better result.
            materialGains = await materials.AwardCellDrops(
                playerId,
                newCells.Select(c => new GridCell(c.GridLat, c.GridLng)).ToList(),
                ct);

            grant.AdventurerXpEarned += milestone.AdventurerXpEarned;
            grant.AdventurerLevel = milestone.AdventurerLevel;
            grant.AdventurerXp = milestone.AdventurerXp;
            grant.AdventurerLevelledUp |= milestone.AdventurerLevelledUp;
            grant.BonusPointsGranted += milestone.BonusPointsGranted;
            grant.Unlocks.AddRange(milestone.Unlocks);
        }

        return new FogRevealResult
        {
            NewCells = newCells,
            XpEarned = (int)(grant?.SkillXpEarned ?? 0),
            Grant = grant,
            Materials = materialGains,
            SkillTraining = skillTraining_,
        };
    }

    /// <summary>
    /// Load all revealed cells for a player (for app startup / full sync).
    /// </summary>
    public async Task<List<CellDto>> GetAllRevealed(int playerId, CancellationToken ct)
    {
        return await db.RevealedCells
            .Where(r => r.PlayerId == playerId)
            .Select(r => new CellDto
            {
                GridLat = r.GridLat,
                GridLng = r.GridLng,
                South = r.GridLat * CellSize,
                West = r.GridLng * CellSize,
                North = r.GridLat * CellSize + CellSize,
                East = r.GridLng * CellSize + CellSize,
            })
            .ToListAsync(ct);
    }
}

public class CellDto
{
    public int GridLat { get; set; }
    public int GridLng { get; set; }
    public double South { get; set; }
    public double West { get; set; }
    public double North { get; set; }
    public double East { get; set; }
}

public class FogRevealResult
{
    public static readonly FogRevealResult Empty = new();

    public List<CellDto> NewCells { get; set; } = [];

    /// <summary>
    /// Total skill XP awarded across every skill trained this reveal.
    ///
    /// Stage 04 made this a sum rather than Exploration's alone: cells now train every
    /// skill whose terrain mapping matches, so singling one out would misreport the walk.
    /// The per-skill breakdown is in <see cref="SkillTraining"/>.
    /// </summary>
    public int XpEarned { get; set; }

    /// <summary>Full progression outcome — levels, Bonus Points and unlocks (§3.1c).</summary>
    public XpGrantResult? Grant { get; set; }

    /// <summary>Materials picked up, so the app can show the pickups (Stage 03 task 5).</summary>
    public List<MaterialGainDto> Materials { get; set; } = [];

    /// <summary>Per-skill XP earned from the revealed cells (Stage 04).</summary>
    public List<SkillTrainingDto> SkillTraining { get; set; } = [];
}
