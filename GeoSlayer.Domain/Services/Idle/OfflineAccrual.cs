using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Idle
{
    /// <summary>
    /// What a worker produced while the app was closed (DESIGN.md §5.3).
    ///
    /// <para><b>Lazy, never ticked.</b> A ticking model costs O(players × workers) forever,
    /// including for everyone asleep; this costs O(workers) once, only for players who
    /// actually return. There is deliberately no Hangfire job.</para>
    ///
    /// <para>Pure and static so the rate rules — especially the terrain multiplier, which is
    /// the load-bearing one — can be asserted directly.</para>
    /// </summary>
    public static class OfflineAccrual
    {
        /// <summary>Base offline cap before upgrades, in hours (§5.2).</summary>
        public const double BaseOfflineCapHours = 4;

        /// <summary>
        /// Yield and XP multiplier when a worker's skill matches its Claim's terrain.
        ///
        /// §5.2 gives "+50% to +100%"; the midpoint keeps varied territory clearly better
        /// without making mismatched terrain feel pointless.
        /// </summary>
        public const double MatchingTerrainMultiplier = 1.75;

        /// <summary>
        /// Skill XP per hour for a tier-1 worker.
        ///
        /// <para>Deliberately far below walking. A walk through matching terrain pays ~3 XP
        /// per revealed cell and a moderate walk reveals hundreds, so an hour on foot is
        /// worth many hours of idling. §5.2: "if a player can rationally decide to stop
        /// walking because the workers have it covered, the rate is wrong."</para>
        /// </summary>
        public const double BaseXpPerHour = 6;

        /// <summary>Material units per hour for a tier-1 worker.</summary>
        public const double BaseMaterialsPerHour = 2;

        /// <summary>Each tier adds this much to the rate multiplier.</summary>
        public const double PerTierBonus = 0.5;

        /// <summary>
        /// Food units one worker consumes per hour (DESIGN.md §5.2, deferred from Stage 05
        /// until Cooking existed to supply it).
        ///
        /// <para>Deliberately low relative to output: upkeep is a material <i>sink</i> against
        /// infinite stockpiling, not a tax that makes workers not worth running. At 4 units
        /// per 4-hour cycle against ~8 material units produced, a worker still nets
        /// positive.</para>
        /// </summary>
        public const double FoodPerHour = 1.0;

        /// <summary>
        /// Food needed for an accrual window. Rounded up, so a partial hour still costs
        /// something and the sink cannot be dodged by syncing constantly.
        /// </summary>
        public static int FoodRequired(TimeSpan elapsed) =>
            elapsed <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(elapsed.TotalHours * FoodPerHour);

        /// <summary>What one worker accrued over an elapsed period.</summary>
        public readonly record struct Accrual(
            TimeSpan Elapsed,
            bool WasCapped,
            double SkillXp,
            double MaterialUnits,
            double TerrainMultiplier);

        /// <summary>
        /// Elapsed time that actually counts, capped (§5.3).
        ///
        /// The cap is the anti-degeneracy lever: you must come back, but you need not be
        /// glued. It never punishes the player for sleeping — it simply stops paying.
        /// </summary>
        public static TimeSpan CappedElapsed(DateTime lastCollectedUtc, DateTime nowUtc, double capHours)
        {
            var elapsed = nowUtc - lastCollectedUtc;

            // A clock moving backwards must not produce negative yield.
            if (elapsed < TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            var cap = TimeSpan.FromHours(Math.Max(0, capHours));

            return elapsed > cap ? cap : elapsed;
        }

        /// <summary>
        /// Whether a worker's assigned skill benefits from its Claim's terrain.
        ///
        /// <para>A mismatch is <b>not</b> a failure — it means base rate. Returning false
        /// here must never be read as "cannot work" (§5.2).</para>
        /// </summary>
        public static bool TerrainMatches(TerrainType claimTerrain, TerrainType skillTerrains)
        {
            if (claimTerrain == TerrainType.Open || skillTerrains == TerrainType.Open)
            {
                return false;
            }

            return (claimTerrain & skillTerrains) != 0;
        }

        /// <summary>
        /// Compute one worker's accrual.
        /// </summary>
        /// <param name="skillTerrains">
        /// Terrain flags this worker's skill is good at, unioned. <see cref="TerrainType.Open"/>
        /// means the skill has no terrain preference.
        /// </param>
        public static Accrual Compute(
            DateTime lastCollectedUtc,
            DateTime nowUtc,
            double capHours,
            int tier,
            TerrainType claimTerrain,
            TerrainType skillTerrains)
        {
            var rawElapsed = nowUtc - lastCollectedUtc;
            var elapsed = CappedElapsed(lastCollectedUtc, nowUtc, capHours);

            var wasCapped = rawElapsed > TimeSpan.FromHours(Math.Max(0, capHours));

            if (elapsed <= TimeSpan.Zero)
            {
                return new Accrual(TimeSpan.Zero, wasCapped, 0, 0, 1.0);
            }

            // Terrain multiplies. It never gates — a worker on mismatched ground still
            // produces, at base rate. This is the rule §5.2 calls load-bearing.
            var terrainMultiplier = TerrainMatches(claimTerrain, skillTerrains)
                ? MatchingTerrainMultiplier
                : 1.0;

            var tierMultiplier = 1 + Math.Max(0, tier - 1) * PerTierBonus;

            var hours = elapsed.TotalHours;

            return new Accrual(
                elapsed,
                wasCapped,
                BaseXpPerHour * hours * tierMultiplier * terrainMultiplier,
                BaseMaterialsPerHour * hours * tierMultiplier * terrainMultiplier,
                terrainMultiplier);
        }

        /// <summary>
        /// When a worker will hit its cap, for scheduling a one-shot "workers idle"
        /// notification (§5.3) rather than polling for it.
        /// </summary>
        public static DateTime CapReachedAtUtc(DateTime lastCollectedUtc, double capHours) =>
            lastCollectedUtc.AddHours(Math.Max(0, capHours));
    }
}
