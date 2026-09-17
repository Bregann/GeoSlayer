namespace GeoSlayer.Domain.Services.Fog
{
    /// <summary>
    /// Speed-graded reveal versus transit (DESIGN.md §7.1).
    ///
    /// <para>The rejected fix was a hard cap above ~18 km/h. §7.1 rejects it because it
    /// "punishes cyclists, who are a legitimate audience moving under their own power", and
    /// gives a bus commuter nothing for genuinely passing through new territory. A rule that
    /// cannot tell a cyclist from a passenger is too blunt.</para>
    ///
    /// <para>So: full reveal at walking pace, partial at cycling pace, pure transit above —
    /// which handles cyclists without a special case.</para>
    /// </summary>
    public static class TransitGrading
    {
        /// <summary>Up to this, it is a walk. Full reveal. ~7.2 km/h.</summary>
        public const double WalkingPaceMetresPerSecond = 2.0;

        /// <summary>Up to this, it is a ride under your own power. ~27 km/h.</summary>
        public const double CyclingPaceMetresPerSecond = 7.5;

        /// <summary>Cells banked per day, so a long-haul flight does not bank a continent.</summary>
        public const int DailyTransitCap = 500;

        /// <summary>One walked cell redeems this many banked ones (§7.1: "2–3").</summary>
        public const int RedeemedPerWalkedCell = 3;

        /// <summary>Metres within which walking redeems banked transit.</summary>
        public const double RedemptionRadiusMetres = 400;

        /// <summary>Unredeemed transit expires after this. An opportunity, not an obligation.</summary>
        public static readonly TimeSpan DecayWindow = TimeSpan.FromDays(7);

        /// <summary>What a stretch of movement is worth.</summary>
        public enum Grade
        {
            /// <summary>Full reveal.</summary>
            Reveal,

            /// <summary>Partial: reveals, and banks the remainder.</summary>
            Mixed,

            /// <summary>Banks only. Nothing reveals.</summary>
            Transit,
        }

        /// <summary>Grade a speed.</summary>
        public static Grade GradeFor(double metresPerSecond)
        {
            if (metresPerSecond <= WalkingPaceMetresPerSecond)
            {
                return Grade.Reveal;
            }

            if (metresPerSecond <= CyclingPaceMetresPerSecond)
            {
                return Grade.Mixed;
            }

            return Grade.Transit;
        }

        /// <summary>
        /// How much of a cell reveals at this speed, 0–1.
        ///
        /// <para>Tapers linearly through the cycling band rather than stepping, so a cyclist
        /// slowing for a hill is not punished by a cliff edge.</para>
        /// </summary>
        public static double RevealFraction(double metresPerSecond)
        {
            if (metresPerSecond <= WalkingPaceMetresPerSecond)
            {
                return 1.0;
            }

            if (metresPerSecond > CyclingPaceMetresPerSecond)
            {
                return 0.0;
            }

            var band = CyclingPaceMetresPerSecond - WalkingPaceMetresPerSecond;
            var into = metresPerSecond - WalkingPaceMetresPerSecond;

            return Math.Clamp(1.0 - into / band, 0.0, 1.0);
        }

        /// <summary>What a cell at this speed banks as transit — the complement of its reveal.</summary>
        public static double TransitWeight(double metresPerSecond) =>
            1.0 - RevealFraction(metresPerSecond);
    }
}
