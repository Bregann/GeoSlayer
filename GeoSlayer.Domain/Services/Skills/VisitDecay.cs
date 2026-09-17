namespace GeoSlayer.Domain.Services.Skills
{
    /// <summary>
    /// Diminishing returns on revisiting a POI (DESIGN.md §3.4).
    ///
    /// <para>§3.4 calls this "the single most important anti-degeneracy rule": without it the
    /// optimal play is to stand at one pub and tap, which is the opposite of an exploration
    /// game. Decay makes <b>new ground strictly better than old ground</b>.</para>
    ///
    /// <para>Pure and static so the curve can be asserted directly rather than inferred from
    /// a database fixture.</para>
    /// </summary>
    public static class VisitDecay
    {
        /// <summary>
        /// Never falls below 5%, so a local POI is never literally worthless — but never a
        /// farm either.
        /// </summary>
        public const double Floor = 0.05;

        /// <summary>One visit charge is regained per 24 hours (§3.4).</summary>
        public const double HoursPerChargeRegained = 24;

        /// <summary>
        /// <c>decay(n) = max(0.05, 1 / (1 + 0.5n))</c> — the formula verbatim from §3.4.
        ///
        /// First visit full, second 67%, fifth ~29%.
        /// </summary>
        public static double Multiplier(int visitCount)
        {
            // A negative count would invert the curve and pay more than full.
            if (visitCount <= 0) return 1.0;

            return Math.Max(Floor, 1.0 / (1.0 + 0.5 * visitCount));
        }

        /// <summary>
        /// The decay-relevant visit count after <paramref name="elapsed"/> has passed,
        /// regaining one charge per 24 hours.
        ///
        /// Floored at zero: enough time away restores a POI to full value, which is what
        /// makes returning to a place months later feel worthwhile again.
        /// </summary>
        public static int DecayedCount(int storedCount, TimeSpan elapsed)
        {
            if (storedCount <= 0) return 0;

            var regained = (int)Math.Floor(elapsed.TotalHours / HoursPerChargeRegained);

            return Math.Max(0, storedCount - Math.Max(0, regained));
        }

        /// <summary>
        /// XP for a visit, given the POI's base reward and the player's decayed visit count.
        /// </summary>
        public static long XpForVisit(int baseXpReward, int decayedVisitCount)
        {
            var value = baseXpReward * Multiplier(decayedVisitCount);

            // Floor at 1 rather than 0: a visit that grants literally nothing reads as a bug
            // to the player, and §3.4 wants "never worthless", not "eventually zero".
            return Math.Max(1, (long)Math.Floor(value));
        }
    }
}
