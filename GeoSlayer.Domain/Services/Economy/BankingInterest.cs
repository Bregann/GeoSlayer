using GeoSlayer.Domain.Interfaces.Api.Admin;
using GeoSlayer.Domain.Services.Admin;

namespace GeoSlayer.Domain.Services.Economy
{
    /// <summary>
    /// Interest on deposited coin (DESIGN.md §5.4a).
    ///
    /// <para><b>Deliberately small.</b> This is a nudge for locking money away, not an income
    /// stream — a game about walking outside must never make sitting still the efficient
    /// play. The rate is set so a deposit is a mild convenience a player is pleased to notice,
    /// and never a reason to stop going out.</para>
    ///
    /// <para>Pure and static, so the bound on it can be asserted without a database.</para>
    /// </summary>
    public static class BankingInterest
    {
        /// <summary>
        /// Interest per hour on the deposited balance, as a fraction.
        ///
        /// <para>0.1%/hour. Against the offline cap below, a full window pays ~0.4% at base
        /// and ~2.4% at a fully-upgraded 24h — so a 10,000 coin deposit yields 40–240 coin a
        /// day. Real, noticeable, and nowhere near what an hour's walk earns, which is the
        /// whole point.</para>
        ///
        /// <para>Seeded as a constant here rather than in the database because it is the one
        /// number that decides whether the economy stays walk-first; it should be changed
        /// deliberately, with the reasoning above in view.</para>
        /// </summary>
        public const double RatePerHour = 0.001;

        /// <summary>
        /// The live settings table, when one is wired up. See <c>CoinPricing.Settings</c>
        /// for why this is a static hook rather than an injected dependency.
        /// </summary>
        public static IGameSettings? Settings { get; set; }

        /// <summary>The live rate, from the settings table or <see cref="RatePerHour"/>.</summary>
        private static double CurrentRate =>
            Settings?.Get(GameSettingKeys.BankingInterestRate, RatePerHour) ?? RatePerHour;

        /// <summary>
        /// Hours of interest a single absence can earn, before upgrades.
        ///
        /// <para>Matched to <c>OfflineAccrual.BaseOfflineCapHours</c> on purpose. Interest is
        /// idle income, and §5.2 already decided how much idle income one absence may pay:
        /// bounding it the same way means a player who returns daily is not quietly behind
        /// one who leaves the app closed for a month.</para>
        /// </summary>
        public const double BaseCapHours = 4;

        /// <summary>
        /// Interest earned on <paramref name="deposited"/> over an absence.
        ///
        /// <para>Simple, not compounding, and clamped to <paramref name="capHours"/>. Both
        /// choices exist for the same reason: an uncapped compounding balance eventually
        /// dwarfs every other income in the game, and this economy is meant to reward the
        /// walk.</para>
        /// </summary>
        /// <param name="deposited">Current deposited balance.</param>
        /// <param name="elapsed">Time since interest was last settled.</param>
        /// <param name="capHours">
        /// The player's offline cap in hours — base plus Offline Cap upgrades, gear and
        /// buildings, exactly as worker accrual reads it.
        /// </param>
        public static long Accrued(long deposited, TimeSpan elapsed, double capHours)
        {
            if (deposited <= 0 || elapsed <= TimeSpan.Zero)
            {
                return 0;
            }

            var hours = Math.Min(elapsed.TotalHours, Math.Max(0, capHours));

            // Floor, so interest can never round a balance upward for free on a short sync.
            // A player syncing every ten seconds must not out-earn one syncing hourly.
            return (long)Math.Floor(deposited * CurrentRate * hours);
        }

        /// <summary>
        /// What a full window pays, for display before depositing.
        ///
        /// <para>Shown because an invisible bonus is one the player never trusts. Rounding
        /// matches <see cref="Accrued"/> so the preview cannot promise more than it pays.</para>
        /// </summary>
        public static long PerFullWindow(long deposited, double capHours) =>
            Accrued(deposited, TimeSpan.FromHours(Math.Max(0, capHours)), capHours);
    }
}
