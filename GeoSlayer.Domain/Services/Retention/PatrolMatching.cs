using GeoSlayer.Domain.Services.Fog;

namespace GeoSlayer.Domain.Services.Retention
{
    /// <summary>
    /// Detecting a completed patrol circuit (DESIGN.md §5.7).
    ///
    /// <para>Pure and static so the ordering rule — waypoints hit <b>in sequence</b>, not
    /// merely all visited — can be asserted directly. Accepting them out of order would let a
    /// player who happens to live inside the loop claim it without walking it.</para>
    /// </summary>
    public static class PatrolMatching
    {
        /// <summary>Metres within which a waypoint counts as reached.</summary>
        public const double WaypointRadiusMetres = 60;

        /// <summary>A position on a walked path.</summary>
        public readonly record struct Fix(double Latitude, double Longitude);

        /// <summary>
        /// Whether <paramref name="path"/> completes the circuit.
        ///
        /// <para>Walks the path forward, advancing a pointer as each waypoint is reached in
        /// order. Extra wandering between waypoints is fine — the route is a loop to follow,
        /// not a line to trace exactly.</para>
        /// </summary>
        public static bool CompletesCircuit(
            IReadOnlyList<Fix> path,
            IReadOnlyList<Fix> waypoints,
            double radiusMetres = WaypointRadiusMetres)
        {
            if (waypoints.Count == 0) return false;
            if (path.Count == 0) return false;

            var next = 0;

            foreach (var fix in path)
            {
                if (next >= waypoints.Count) break;

                var distance = TraceValidator.HaversineMetres(
                    fix.Latitude, fix.Longitude,
                    waypoints[next].Latitude, waypoints[next].Longitude);

                if (distance <= radiusMetres) next++;
            }

            return next >= waypoints.Count;
        }

        /// <summary>
        /// How many waypoints have been reached so far, for a progress display.
        /// </summary>
        public static int WaypointsReached(
            IReadOnlyList<Fix> path,
            IReadOnlyList<Fix> waypoints,
            double radiusMetres = WaypointRadiusMetres)
        {
            var next = 0;

            foreach (var fix in path)
            {
                if (next >= waypoints.Count) break;

                var distance = TraceValidator.HaversineMetres(
                    fix.Latitude, fix.Longitude,
                    waypoints[next].Latitude, waypoints[next].Longitude);

                if (distance <= radiusMetres) next++;
            }

            return next;
        }

        /// <summary>
        /// Food awarded for completing a circuit.
        ///
        /// <para>Scales with waypoint count so a longer loop pays more, and is deliberately
        /// upkeep rather than XP: §5.7 keeps novelty as the only route to <i>progress</i> and
        /// makes routine the route to <i>maintenance</i>.</para>
        /// </summary>
        public static int UpkeepReward(int waypointCount) => Math.Max(2, waypointCount * 2);
    }
}
