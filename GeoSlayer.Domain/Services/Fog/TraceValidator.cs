using GeoSlayer.Domain.DTOs.Journey.Requests;

namespace GeoSlayer.Domain.Services.Fog
{
    /// <summary>Why a batch, or part of one, was not allowed to reveal.</summary>
    public enum TraceRejection
    {
        None = 0,

        /// <summary>Nothing usable left after the accuracy cutoff.</summary>
        NoUsablePositions,

        /// <summary>Net displacement far below path length — a random walk, not travel.</summary>
        Drift,

        /// <summary>The player has not actually gone anywhere for several minutes.</summary>
        Dwell,

        /// <summary>Moving faster than a walk or run — vehicle travel reveals nothing (yet).</summary>
        TooFast,
    }

    /// <summary>The verdict on one sync batch.</summary>
    public record TraceVerdict(TraceRejection Rejection, IReadOnlyList<SyncPosition> Accepted)
    {
        public bool Allowed => Rejection == TraceRejection.None && Accepted.Count > 0;

        public static TraceVerdict Reject(TraceRejection why) => new(why, []);
    }

    /// <summary>
    /// Decides how much of a reported path is real travel.
    ///
    /// Swept-path reveal amplifies both of the exploits this guards against — a phone drifting
    /// on a desk would paint a neighbourhood, and a car would paint a city — so these checks
    /// are not optional decoration around task 2, they are what makes it safe.
    ///
    /// Pure and clock-free: every input comes from the batch itself, so it unit tests directly.
    /// </summary>
    public static class TraceValidator
    {
        /// <summary>
        /// Worst horizontal accuracy we will accept, in metres.  Beyond this the fix could be
        /// a street away and the swept line would be fiction.
        /// </summary>
        public const double MaxAccuracyMetres = 25.0;

        /// <summary>
        /// Minimum ratio of net displacement to total path length.  A straight walk is ~1.0;
        /// a stationary phone's GPS jitter is near 0.  Below this the batch is drift.
        /// </summary>
        public const double MinDisplacementRatio = 0.3;

        /// <summary>Path length below which the displacement ratio is meaningless noise.</summary>
        public const double MinPathLengthForRatioMetres = 30.0;

        /// <summary>A centroid that stays inside this radius counts as "not going anywhere".</summary>
        public const double DwellRadiusMetres = 20.0;

        /// <summary>How long the player must be inside <see cref="DwellRadiusMetres"/> to be dwelling.</summary>
        public const double DwellSeconds = 180.0;

        /// <summary>
        /// Fastest pace that still reveals.  18 km/h = 5 m/s — a quick run, below any vehicle.
        /// Above this, reveal nothing; Uncharted Transit banking arrives in Stage 14.
        /// </summary>
        public const double MaxRevealSpeedMetresPerSecond = 5.0;

        // ── Geometry ────────────────────────────────────────────────────

        public static double HaversineMetres(double lat1, double lon1, double lat2, double lon2)
        {
            const double r = 6_371_000;

            var dLat = (lat2 - lat1) * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        /// <summary>Total distance walked along the path, summed leg by leg.</summary>
        public static double PathLengthMetres(IReadOnlyList<SyncPosition> path)
        {
            double total = 0;

            for (var i = 1; i < path.Count; i++)
                total += HaversineMetres(
                    path[i - 1].Latitude, path[i - 1].Longitude,
                    path[i].Latitude, path[i].Longitude);

            return total;
        }

        /// <summary>Straight-line distance from the first fix to the last.</summary>
        public static double NetDisplacementMetres(IReadOnlyList<SyncPosition> path) =>
            path.Count < 2
                ? 0
                : HaversineMetres(
                    path[0].Latitude, path[0].Longitude,
                    path[^1].Latitude, path[^1].Longitude);

        // ── Validation ──────────────────────────────────────────────────

        /// <summary>
        /// Filter and judge a batch.  Returns the positions that may reveal, or the reason
        /// the batch may not.
        /// </summary>
        /// <summary>
        /// Faster than any vehicle a player could plausibly be in — 400 km/h.
        ///
        /// <para>Above this it is not a commute, it is a forged path. Everything below banks
        /// as Uncharted Transit instead of being rejected (§7.1).</para>
        /// </summary>
        public const double ImplausibleSpeedMetresPerSecond = 111.0;

        public static TraceVerdict Validate(IReadOnlyList<SyncPosition> path)
        {
            // ── Accuracy cutoff ──────────────────────────────────────
            // Before anything else: a fix we do not trust cannot inform any later check.
            //
            // A missing accuracy passes.  It is the weaker choice, but clients that predate
            // task 4 do not send one at all, and rejecting those would mean no client
            // reveals anything until the app ships. The motion checks below still apply to
            // them, so a drifting or driving legacy client is still caught.
            var usable = path
                .Where(p => p.Accuracy is not { } a || a <= MaxAccuracyMetres)
                .ToList();

            if (usable.Count == 0)
                return TraceVerdict.Reject(TraceRejection.NoUsablePositions);

            // A single usable fix carries no motion information — reveal around it and stop.
            if (usable.Count == 1)
                return new TraceVerdict(TraceRejection.None, usable);

            var elapsedSeconds = ElapsedSeconds(usable);
            var pathLength = PathLengthMetres(usable);
            var displacement = NetDisplacementMetres(usable);

            // ── Speed grading ────────────────────────────────────────
            // Judged on net displacement, not path length: GPS jitter inflates path length
            // and would otherwise flag a slow walk as a vehicle.
            //
            // Stage 14 replaced the hard rejection here with Uncharted Transit (§7.1). A fast
            // batch is no longer thrown away — it banks instead, because rejecting it
            // "punishes cyclists, who are a legitimate audience moving under their own power"
            // and gives a commuter nothing for genuinely passing through new ground.
            //
            // The old TooFast rejection is kept only for speeds no human achieves under any
            // power, which is a spoofing signal rather than a commute.
            if (elapsedSeconds > 0 && displacement / elapsedSeconds > ImplausibleSpeedMetresPerSecond)
                return TraceVerdict.Reject(TraceRejection.TooFast);

            // ── Dwell detection ──────────────────────────────────────
            // A phone sitting on a desk: plenty of jitter, no actual travel.
            if (elapsedSeconds >= DwellSeconds && displacement < DwellRadiusMetres)
                return TraceVerdict.Reject(TraceRejection.Dwell);

            // ── Displacement gate ────────────────────────────────────
            // A random walk wanders far in total but ends where it began.  Only meaningful
            // once the path is long enough that the ratio is not noise.
            if (pathLength >= MinPathLengthForRatioMetres &&
                displacement / pathLength < MinDisplacementRatio)
                return TraceVerdict.Reject(TraceRejection.Drift);

            return new TraceVerdict(TraceRejection.None, usable);
        }

        /// <summary>
        /// Seconds spanned by the batch according to the client clock.  Negative spans (a
        /// clock jumping backwards, or positions out of order) count as zero.
        /// </summary>
        public static double ElapsedSeconds(IReadOnlyList<SyncPosition> path)
        {
            if (path.Count < 2)
                return 0;

            var span = (path[^1].TimestampMs - path[0].TimestampMs) / 1000.0;
            return span > 0 ? span : 0;
        }
    }
}
