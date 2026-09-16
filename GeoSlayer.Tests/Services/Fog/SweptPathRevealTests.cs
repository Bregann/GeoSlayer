using GeoSlayer.Domain.DTOs.Journey.Requests;
using GeoSlayer.Domain.Services;
using GeoSlayer.Domain.Services.Fog;

namespace GeoSlayer.Tests.Services.Fog;

/// <summary>
/// The sweep as the service uses it: real lat/lng traces projected onto the fog grid.
/// Pure geometry — no database, so these run without Docker.
/// </summary>
[TestFixture]
public class SweptPathRevealTests
{
    /// <summary>Roughly central London — a plausible play area, well away from the poles.</summary>
    private const double OriginLat = 51.5074;
    private const double OriginLng = -0.1278;

    private const double MetresPerDegreeLat = 111_320.0;

    private static double LatOffset(double metres) => metres / MetresPerDegreeLat;

    private static double LngOffset(double metres, double atLat) =>
        metres / (MetresPerDegreeLat * Math.Cos(atLat * Math.PI / 180.0));

    /// <summary>The cells the service would reveal for a trace, sweep plus 3×3 dilation.</summary>
    private static HashSet<GridCell> Revealed(IEnumerable<SyncPosition> path, int radius = 1)
    {
        var grid = path
            .Select(p => new GridCell(FogService.ToGrid(p.Latitude), FogService.ToGrid(p.Longitude)))
            .ToList();

        return PathSweep.Dilate(PathSweep.SweepPath(grid), radius);
    }

    /// <summary>A straight walk due east at 1.4 m/s, one fix per <paramref name="fixInterval"/>.</summary>
    private static List<SyncPosition> WalkEast(double metres, double fixIntervalSeconds)
    {
        const double speed = 1.4;
        var positions = new List<SyncPosition>();
        var start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var step = speed * fixIntervalSeconds;

        // Always finish exactly at `metres`, whatever the fix interval — otherwise
        // traces at different sampling rates end in different cells and can't be compared.
        for (double travelled = 0; ; travelled = Math.Min(travelled + step, metres))
        {
            positions.Add(new SyncPosition
            {
                Latitude = OriginLat,
                Longitude = OriginLng + LngOffset(travelled, OriginLat),
                TimestampMs = start + (long)(travelled / speed * 1000),
                Accuracy = 8,
            });

            if (travelled >= metres) break;
        }

        return positions;
    }

    // ── Acceptance criterion 5 ──────────────────────────────────────

    [Test]
    public void Sweep_FiveHundredMetreWalk_RevealsAContiguousCorridor()
    {
        // Sparse fixes: one every 30s, so ~42 m apart — further than a cell is wide.
        // Without the sweep this would leave holes between fixes.
        var revealed = Revealed(WalkEast(500, fixIntervalSeconds: 30));

        Assert.That(IsFourConnected(revealed), Is.True, "the corridor has gaps");

        // 500 m due east at this latitude spans ~8.7 cells of 0.0009° (~62 m here),
        // dilated to 3 cells tall.
        Assert.That(revealed.Select(c => c.GridLat).Distinct().Count(), Is.EqualTo(3),
            "a due-east walk should be exactly three cells tall after 3x3 dilation");
        Assert.That(revealed, Has.Count.GreaterThan(20));
    }

    [Test]
    public void Sweep_SparseFixes_RevealsTheSameCorridorAsDenseFixes()
    {
        // The whole point of the sweep: sampling rate must not change the map.
        var dense = Revealed(WalkEast(500, fixIntervalSeconds: 2));
        var sparse = Revealed(WalkEast(500, fixIntervalSeconds: 60));

        Assert.That(sparse, Is.EquivalentTo(dense));
    }

    [Test]
    public void Sweep_SinglePointPath_StillRevealsTheThreeByThreeBlock()
    {
        // Legacy single-point clients must keep working.
        var revealed = Revealed([
            new SyncPosition { Latitude = OriginLat, Longitude = OriginLng },
        ]);

        Assert.That(revealed, Has.Count.EqualTo(9));
    }

    [Test]
    public void Sweep_ReplayingTheSamePath_YieldsNoCellsNotAlreadyRevealed()
    {
        // Acceptance criterion 8, at the geometry level: the second run of a path
        // produces exactly the cells the first already covered, so the service's
        // "skip what exists" filter leaves nothing to insert and no XP to grant.
        var path = WalkEast(500, fixIntervalSeconds: 10);

        var first = Revealed(path);
        var second = Revealed(path);

        Assert.That(second.Except(first), Is.Empty);
    }

    [Test]
    public void Sweep_PathRevisitingItsOwnCells_Deduplicates()
    {
        // There and back again reveals the corridor once, not twice.
        var out_ = WalkEast(200, fixIntervalSeconds: 10);
        var there = Revealed(out_);
        var thereAndBack = Revealed(out_.Concat(Enumerable.Reverse(out_)));

        Assert.That(thereAndBack, Is.EquivalentTo(there));
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static bool IsFourConnected(IReadOnlyCollection<GridCell> cells)
    {
        if (cells.Count == 0) return true;

        var remaining = cells.ToHashSet();
        var start = remaining.First();
        var queue = new Queue<GridCell>();

        queue.Enqueue(start);
        remaining.Remove(start);

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();

            foreach (var neighbour in new[]
            {
                new GridCell(cell.GridLat + 1, cell.GridLng),
                new GridCell(cell.GridLat - 1, cell.GridLng),
                new GridCell(cell.GridLat, cell.GridLng + 1),
                new GridCell(cell.GridLat, cell.GridLng - 1),
            })
            {
                if (remaining.Remove(neighbour))
                    queue.Enqueue(neighbour);
            }
        }

        return remaining.Count == 0;
    }
}
