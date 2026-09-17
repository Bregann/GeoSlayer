using GeoSlayer.Domain.DTOs.Journey.Requests;
using GeoSlayer.Domain.Services;
using GeoSlayer.Domain.Services.Fog;

namespace GeoSlayer.Tests.Services.Fog;

/// <summary>
/// The anti-cheat gate, driven by synthetic traces standing in for the real-world
/// behaviours it exists to separate: a walk, a phone on a desk, and a car.
/// </summary>
[TestFixture]
public class TraceValidatorTests
{
    private const double OriginLat = 51.5074;
    private const double OriginLng = -0.1278;
    private const double MetresPerDegreeLat = 111_320.0;

    private static double LatOffset(double metres) => metres / MetresPerDegreeLat;

    private static double LngOffset(double metres, double atLat) =>
        metres / (MetresPerDegreeLat * Math.Cos(atLat * Math.PI / 180.0));

    /// <summary>Cells that would actually be revealed for a trace, gate included.</summary>
    private static int RevealedCellCount(IReadOnlyList<SyncPosition> trace)
    {
        var verdict = TraceValidator.Validate(trace);
        if (!verdict.Allowed) return 0;

        var grid = verdict.Accepted
            .Select(p => new GridCell(FogService.ToGrid(p.Latitude), FogService.ToGrid(p.Longitude)))
            .ToList();

        return PathSweep.Dilate(PathSweep.SweepPath(grid), 1).Count;
    }

    // ── Trace generators ────────────────────────────────────────────

    /// <summary>A steady walk on a consistent bearing at <paramref name="speed"/> m/s.</summary>
    private static List<SyncPosition> StraightTrace(
        double speed, int fixes, double fixIntervalSeconds, double accuracy = 8)
    {
        var start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return Enumerable.Range(0, fixes).Select(i =>
        {
            var travelled = speed * fixIntervalSeconds * i;

            return new SyncPosition
            {
                Latitude = OriginLat,
                Longitude = OriginLng + LngOffset(travelled, OriginLat),
                TimestampMs = start + (long)(i * fixIntervalSeconds * 1000),
                Accuracy = accuracy,
            };
        }).ToList();
    }

    /// <summary>
    /// A phone on a desk: GPS jitter inside a small radius, going nowhere.
    /// Seeded so a failure is reproducible.
    /// </summary>
    private static List<SyncPosition> DriftTrace(
        int fixes, double fixIntervalSeconds, double radiusMetres = 12, int seed = 1234)
    {
        var rng = new Random(seed);
        var start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return Enumerable.Range(0, fixes).Select(i =>
        {
            var angle = rng.NextDouble() * 2 * Math.PI;
            var distance = rng.NextDouble() * radiusMetres;

            return new SyncPosition
            {
                Latitude = OriginLat + LatOffset(distance * Math.Sin(angle)),
                Longitude = OriginLng + LngOffset(distance * Math.Cos(angle), OriginLat),
                TimestampMs = start + (long)(i * fixIntervalSeconds * 1000),
                Accuracy = 15,
            };
        }).ToList();
    }

    // ── Required: the walk reveals ──────────────────────────────────

    [Test]
    public void Validate_WalkingTrace_IsAllowed()
    {
        // 1.4 m/s on a consistent bearing for 5 minutes — the canonical real player.
        var trace = StraightTrace(speed: 1.4, fixes: 60, fixIntervalSeconds: 5);

        var verdict = TraceValidator.Validate(trace);

        Assert.That(verdict.Allowed, Is.True);
        Assert.That(verdict.Rejection, Is.EqualTo(TraceRejection.None));
        Assert.That(verdict.Accepted, Has.Count.EqualTo(60));
    }

    [Test]
    public void Validate_WalkingTrace_RevealsTheExpectedCellCount()
    {
        // 1.4 m/s × 295 s ≈ 413 m due east.  At this latitude a cell is ~62 m wide, so
        // ~7.7 cells of sweep, +1 either end from dilation, × 3 rows tall.
        var trace = StraightTrace(speed: 1.4, fixes: 60, fixIntervalSeconds: 5);

        var cells = RevealedCellCount(trace);

        Assert.That(cells, Is.InRange(27, 36),
            "a ~413 m due-east walk should reveal roughly a 9-11 by 3 corridor");
    }

    // ── Required: the desk reveals nothing ──────────────────────────

    [Test]
    public void Validate_DriftTrace_RevealsZeroCells()
    {
        // Ten minutes of jitter inside 12 m — a phone on a desk, backgrounded.
        var trace = DriftTrace(fixes: 120, fixIntervalSeconds: 5);

        Assert.That(RevealedCellCount(trace), Is.Zero);
    }

    [Test]
    public void Validate_DriftTrace_IsRejectedAsDwellOrDrift()
    {
        var verdict = TraceValidator.Validate(DriftTrace(fixes: 120, fixIntervalSeconds: 5));

        Assert.That(verdict.Allowed, Is.False);
        Assert.That(verdict.Rejection,
            Is.EqualTo(TraceRejection.Dwell).Or.EqualTo(TraceRejection.Drift));
    }

    [Test]
    public void Validate_DriftTraceAtVariousSeeds_NeverReveals()
    {
        // Not a lucky seed: no random walk inside 25 m should ever paint the map.
        for (var seed = 0; seed < 25; seed++)
        {
            var trace = DriftTrace(fixes: 120, fixIntervalSeconds: 5, radiusMetres: 20, seed: seed);

            Assert.That(RevealedCellCount(trace), Is.Zero, $"seed {seed} revealed cells");
        }
    }

    [Test]
    public void Validate_ShortDriftBurst_IsRejectedAsDrift()
    {
        // Too short to count as dwelling, but still a random walk going nowhere.
        var verdict = TraceValidator.Validate(DriftTrace(fixes: 20, fixIntervalSeconds: 2));

        Assert.That(verdict.Allowed, Is.False);
        Assert.That(verdict.Rejection, Is.EqualTo(TraceRejection.Drift));
    }

    // ── Required: the car reveals nothing ───────────────────────────

    [Test]
    public void Validate_DrivingTrace_IsAcceptedButGradedAsTransit()
    {
        // 15 m/s ≈ 54 km/h — clearly a vehicle.
        //
        // Stage 14 replaced the hard rejection with Uncharted Transit (§7.1). The batch is
        // no longer thrown away — the validator accepts it, and FogService banks it rather
        // than revealing, because rejecting outright "gives a bus commuter nothing for
        // genuinely passing through new territory".
        var trace = StraightTrace(speed: 15, fixes: 40, fixIntervalSeconds: 5);

        var verdict = TraceValidator.Validate(trace);

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Allowed, Is.True, "a commute is no longer rejected outright");
            Assert.That(TransitGrading.GradeFor(15), Is.EqualTo(TransitGrading.Grade.Transit));
            Assert.That(TransitGrading.RevealFraction(15), Is.Zero, "but it reveals nothing");
        });
    }

    [Test]
    public void Validate_ImplausibleSpeed_IsStillRejected()
    {
        // Above any speed a player could plausibly be travelling at — 150 m/s is 540 km/h.
        // That is not a commute, it is a forged path, so the TooFast rejection survives
        // for exactly this case.
        var verdict = TraceValidator.Validate(StraightTrace(speed: 150, fixes: 40, fixIntervalSeconds: 5));

        Assert.That(verdict.Rejection, Is.EqualTo(TraceRejection.TooFast));
    }

    [Test]
    public void Validate_RunningTrace_StillReveals()
    {
        // 3.5 m/s ≈ 12.6 km/h — a runner is a player, not a vehicle.
        var verdict = TraceValidator.Validate(StraightTrace(speed: 3.5, fixes: 40, fixIntervalSeconds: 5));

        Assert.That(verdict.Allowed, Is.True);
    }

    [Test]
    public void Validate_JustBelowTheSpeedCut_Reveals()
    {
        var verdict = TraceValidator.Validate(StraightTrace(speed: 4.8, fixes: 30, fixIntervalSeconds: 5));

        Assert.That(verdict.Allowed, Is.True);
    }

    [Test]
    public void Validate_CyclingPace_RevealsPartiallyRatherThanBeingRejected()
    {
        // 5.5 m/s ≈ 20 km/h — a cyclist, moving under their own power. §7.1 rejects the
        // hard cap precisely because it "punishes cyclists, who are a legitimate
        // audience", so this must grade as Mixed rather than being thrown away.
        var verdict = TraceValidator.Validate(StraightTrace(speed: 5.5, fixes: 30, fixIntervalSeconds: 5));

        var fraction = TransitGrading.RevealFraction(5.5);

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Allowed, Is.True);
            Assert.That(TransitGrading.GradeFor(5.5), Is.EqualTo(TransitGrading.Grade.Mixed));
            Assert.That(fraction, Is.GreaterThan(0), "a cyclist reveals something");
            Assert.That(fraction, Is.LessThan(1), "but not as much as a walker");
        });
    }

    // ── Accuracy cutoff ─────────────────────────────────────────────

    [Test]
    public void Validate_AllPositionsTooInaccurate_IsRejected()
    {
        var trace = StraightTrace(speed: 1.4, fixes: 30, fixIntervalSeconds: 5, accuracy: 60);

        var verdict = TraceValidator.Validate(trace);

        Assert.That(verdict.Rejection, Is.EqualTo(TraceRejection.NoUsablePositions));
    }

    [Test]
    public void Validate_InaccuratePositions_AreDroppedNotTheWholeBatch()
    {
        var trace = StraightTrace(speed: 1.4, fixes: 30, fixIntervalSeconds: 5);
        trace[10].Accuracy = 80;
        trace[11].Accuracy = 120;

        var verdict = TraceValidator.Validate(trace);

        Assert.That(verdict.Allowed, Is.True);
        Assert.That(verdict.Accepted, Has.Count.EqualTo(28));
    }

    [Test]
    public void Validate_MissingAccuracy_IsAcceptedForLegacyClients()
    {
        // Clients predating the accuracy field must keep revealing; the motion checks
        // still apply to them.
        var trace = StraightTrace(speed: 1.4, fixes: 30, fixIntervalSeconds: 5);
        foreach (var p in trace) p.Accuracy = null;

        Assert.That(TraceValidator.Validate(trace).Allowed, Is.True);
    }

    [Test]
    public void Validate_SinglePosition_IsAllowed()
    {
        // One fix carries no motion information — reveal around it rather than
        // punishing a client that syncs rarely.
        var verdict = TraceValidator.Validate([
            new SyncPosition { Latitude = OriginLat, Longitude = OriginLng, Accuracy = 10 },
        ]);

        Assert.That(verdict.Allowed, Is.True);
    }

    [Test]
    public void Validate_EmptyPath_IsRejected()
    {
        Assert.That(TraceValidator.Validate([]).Rejection,
            Is.EqualTo(TraceRejection.NoUsablePositions));
    }

    // ── Geometry sanity ─────────────────────────────────────────────

    [Test]
    public void HaversineMetres_KnownShortDistance_IsAccurate()
    {
        // 100 m due north.
        var d = TraceValidator.HaversineMetres(
            OriginLat, OriginLng, OriginLat + LatOffset(100), OriginLng);

        Assert.That(d, Is.EqualTo(100).Within(1.0));
    }

    [Test]
    public void ElapsedSeconds_BackwardsClock_IsClampedToZero()
    {
        var trace = StraightTrace(speed: 1.4, fixes: 3, fixIntervalSeconds: 5);
        (trace[0].TimestampMs, trace[2].TimestampMs) = (trace[2].TimestampMs, trace[0].TimestampMs);

        Assert.That(TraceValidator.ElapsedSeconds(trace), Is.Zero);
    }
}
