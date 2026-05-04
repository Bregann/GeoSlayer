using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Interfaces.Api;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Services;

public class FogService(AppDbContext db) : IFogService
{
    /// <summary>
    /// Grid cell size in degrees.  0.0009° ≈ 100 m at the equator, ~64 m at 45° latitude.
    /// </summary>
    public const double CellSize = 0.0009;

    /// <summary>How many cells around the player to reveal (0 = just the current cell).</summary>
    private const int RevealRadius = 0;

    /// <summary>XP awarded for each newly revealed cell.</summary>
    private const int XpPerCell = 2;

    // ── Anti-cheat constants ───────────────────────────────────────

    /// <summary>Max plausible travel speed (m/s).  50 m/s ≈ 180 km/h — faster than any road vehicle.</summary>
    private const double MaxSpeedMetresPerSecond = 50.0;

    /// <summary>Minimum interval between syncs from the same player.</summary>
    private const double MinSyncIntervalSeconds = 2.0;

    /// <summary>Max cells that can be revealed in a single sync call.</summary>
    private const int MaxNewCellsPerSync = 9;

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

    // ── Haversine ───────────────────────────────────────────────────

    private static double HaversineMetres(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_000;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    // ── Reveal ──────────────────────────────────────────────────────

    /// <summary>
    /// Reveal fog cells around the player's position.
    /// Includes anti-cheat checks: speed cap, sync cooldown, client-clock sanity.
    /// Returns empty result when the sync is rejected.
    /// </summary>
    public async Task<FogRevealResult> Reveal(
        int playerId, double latitude, double longitude, long timestampMs, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var player = await db.Players.FirstAsync(p => p.Id == playerId, ct);

        // ── Anti-cheat: client timestamp sanity ───────────────────
        var clientTime = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime;
        if (Math.Abs((now - clientTime).TotalSeconds) > MaxClientClockSkewSeconds)
            return FogRevealResult.Empty;

        // ── Anti-cheat: sync cooldown ─────────────────────────────
        if (player.LastSyncAtUtc.HasValue)
        {
            var secondsSinceLastSync = (now - player.LastSyncAtUtc.Value).TotalSeconds;
            if (secondsSinceLastSync < MinSyncIntervalSeconds)
                return FogRevealResult.Empty;

            // ── Anti-cheat: speed / distance cap ─────────────────
            if (player.LastLatitude != 0 || player.LastLongitude != 0)
            {
                var distance = HaversineMetres(
                    player.LastLatitude, player.LastLongitude, latitude, longitude);
                var maxAllowed = MaxSpeedMetresPerSecond * secondsSinceLastSync;

                // Allow a 100 m grace buffer for GPS drift
                if (distance > maxAllowed + 100)
                    return FogRevealResult.Empty;
            }
        }

        // ── Update player tracking ────────────────────────────────
        player.LastLatitude = latitude;
        player.LastLongitude = longitude;
        player.LastSyncAtUtc = now;

        // ── Reveal cells ──────────────────────────────────────────
        var centerLat = ToGrid(latitude);
        var centerLng = ToGrid(longitude);

        var candidates = new List<(int lat, int lng)>();
        for (var dLat = -RevealRadius; dLat <= RevealRadius; dLat++)
        for (var dLng = -RevealRadius; dLng <= RevealRadius; dLng++)
            candidates.Add((centerLat + dLat, centerLng + dLng));

        var candidateLats = candidates.Select(c => c.lat).Distinct().ToList();
        var candidateLngs = candidates.Select(c => c.lng).Distinct().ToList();

        var existing = await db.RevealedCells
            .Where(r => r.PlayerId == playerId
                && candidateLats.Contains(r.GridLat)
                && candidateLngs.Contains(r.GridLng))
            .Select(r => new { r.GridLat, r.GridLng })
            .ToListAsync(ct);

        var existingSet = new HashSet<(int, int)>(existing.Select(e => (e.GridLat, e.GridLng)));

        var newCells = new List<CellDto>();

        foreach (var (lat, lng) in candidates)
        {
            if (existingSet.Contains((lat, lng))) continue;
            // Safety cap — ignore excess cells if RevealRadius is ever increased
            if (newCells.Count >= MaxNewCellsPerSync) break;

            db.RevealedCells.Add(new RevealedCell
            {
                PlayerId = playerId,
                GridLat = lat,
                GridLng = lng,
                RevealedAtUtc = now,
            });

            var bounds = CellBounds(lat, lng);
            newCells.Add(new CellDto
            {
                GridLat = lat,
                GridLng = lng,
                South = bounds.south,
                West = bounds.west,
                North = bounds.north,
                East = bounds.east,
            });
        }

        var xpEarned = newCells.Count * XpPerCell;

        if (newCells.Count > 0)
        {
            player.Xp += xpEarned;

            while (player.Xp >= player.Level * 100)
            {
                player.Xp -= player.Level * 100;
                player.Level++;
            }
        }

        return new FogRevealResult
        {
            NewCells = newCells,
            XpEarned = xpEarned,
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
    public int XpEarned { get; set; }
}
