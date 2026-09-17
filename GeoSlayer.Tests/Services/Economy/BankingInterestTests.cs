using GeoSlayer.Domain.Services.Economy;
using GeoSlayer.Domain.Services.Idle;

namespace GeoSlayer.Tests.Services.Economy
{
    /// <summary>
    /// Interest on deposited coin (DESIGN.md §5.4a).
    ///
    /// <para>Most of these assert that interest stays <b>small and bounded</b>. That is the
    /// design requirement, not a tuning preference: a game about walking outside must never
    /// make sitting still the efficient play.</para>
    /// </summary>
    [TestFixture]
    public class BankingInterestTests
    {
        [Test]
        public void ADepositEarnsSomething()
        {
            var earned = BankingInterest.Accrued(
                10_000, TimeSpan.FromHours(4), BankingInterest.BaseCapHours);

            Assert.That(earned, Is.GreaterThan(0), "locking money away should be worth doing");
        }

        [Test]
        public void NoDeposit_EarnsNothing()
        {
            Assert.That(BankingInterest.Accrued(0, TimeSpan.FromHours(24), 24), Is.Zero);
        }

        [Test]
        public void NoTime_EarnsNothing()
        {
            Assert.That(BankingInterest.Accrued(10_000, TimeSpan.Zero, 4), Is.Zero);
        }

        [Test]
        public void NegativeElapsed_EarnsNothing()
        {
            // Clock skew between the app server and the database is not a payday.
            Assert.That(BankingInterest.Accrued(10_000, TimeSpan.FromHours(-5), 4), Is.Zero);
        }

        // ── The bound (§5.4a) ───────────────────────────────────────────

        [Test]
        public void InterestStopsAtTheOfflineCap()
        {
            // The whole safety property. A month away must pay exactly what the cap pays,
            // or leaving the app closed becomes the winning strategy.
            var aDay = BankingInterest.Accrued(100_000, TimeSpan.FromHours(24), capHours: 4);
            var aMonth = BankingInterest.Accrued(100_000, TimeSpan.FromDays(30), capHours: 4);
            var theCap = BankingInterest.Accrued(100_000, TimeSpan.FromHours(4), capHours: 4);

            Assert.Multiple(() =>
            {
                Assert.That(aDay, Is.EqualTo(theCap));
                Assert.That(aMonth, Is.EqualTo(theCap));
            });
        }

        [Test]
        public void ABiggerOfflineCap_EarnsMore()
        {
            // The Offline Cap upgrade must do something here too, or a player who invested in
            // staying away longer is told it only counts for workers.
            var baseCap = BankingInterest.Accrued(100_000, TimeSpan.FromHours(24), capHours: 4);
            var upgraded = BankingInterest.Accrued(100_000, TimeSpan.FromHours(24), capHours: 24);

            Assert.That(upgraded, Is.GreaterThan(baseCap));
        }

        [Test]
        public void InterestDoesNotCompoundWithinAWindow()
        {
            // Simple interest: settling once for four hours must equal the balance growing by
            // the same amount, not more. Compounding on an uncapped balance is how an idle
            // economy runs away.
            const long deposit = 1_000_000;

            var oneSettlement = BankingInterest.Accrued(deposit, TimeSpan.FromHours(4), 4);
            var expected = (long)(deposit * BankingInterest.RatePerHour * 4);

            Assert.That(oneSettlement, Is.EqualTo(expected));
        }

        [Test]
        public void FrequentSyncing_NeverOutEarnsWaiting()
        {
            // Rounding down matters: if each tiny settlement rounded up, a client polling
            // every ten seconds would out-earn one that waited — and the app polls constantly.
            const long deposit = 10_000;

            var inOneGo = BankingInterest.Accrued(deposit, TimeSpan.FromHours(4), 4);

            long piecemeal = 0;
            for (var i = 0; i < 4 * 60 * 6; i++)
            {
                piecemeal += BankingInterest.Accrued(deposit, TimeSpan.FromSeconds(10), 4);
            }

            Assert.That(piecemeal, Is.LessThanOrEqualTo(inOneGo),
                "polling must never beat patience");
        }

        // ── It stays a nudge, not an income ─────────────────────────────

        [Test]
        public void TheRateIsSmall()
        {
            // Stated as a test so raising it is a deliberate act with a failing build, not a
            // quiet edit. A full base window pays well under 1%.
            var fullWindow = BankingInterest.PerFullWindow(100_000, BankingInterest.BaseCapHours);

            Assert.That(fullWindow, Is.LessThan(1_000),
                "a base-cap window should pay under 1% — this is a nudge, not an income");
        }

        [Test]
        public void EvenAFullyUpgradedWindow_StaysModest()
        {
            var fullWindow = BankingInterest.PerFullWindow(100_000, capHours: 24);

            Assert.That(fullWindow, Is.LessThan(5_000),
                "24h at full upgrades should stay well under 5% of the balance");
        }

        [Test]
        public void ADaysWalking_OutEarnsADaysInterest_OnATypicalBalance()
        {
            // The property that matters most. A player with a healthy balance must still make
            // more by going outside than by leaving coin in the bank.
            //
            // A typical mid-game balance, against the base window: interest should be a
            // rounding error next to a single decent haul.
            var interest = BankingInterest.PerFullWindow(50_000, BankingInterest.BaseCapHours);

            Assert.That(interest, Is.LessThan(500),
                "interest on a mid-game balance must not rival a walk");
        }

        [Test]
        public void TheCapMatchesTheWorkerLayer()
        {
            // Two different idle bounds would be two rules for a player to learn, and they
            // would drift. §5.2 already decided how much one absence may pay.
            Assert.That(BankingInterest.BaseCapHours,
                Is.EqualTo(OfflineAccrual.BaseOfflineCapHours));
        }

        [Test]
        public void PerFullWindow_NeverPromisesMoreThanItPays()
        {
            // The preview is shown before depositing, so it must round the same way the
            // payout does.
            const long deposit = 12_345;

            var preview = BankingInterest.PerFullWindow(deposit, 4);
            var actual = BankingInterest.Accrued(deposit, TimeSpan.FromHours(4), 4);

            Assert.That(preview, Is.EqualTo(actual));
        }
    }
}
