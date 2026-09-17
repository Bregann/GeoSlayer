using GeoSlayer.Domain.Services.Economy;
using GeoSlayer.Domain.Services.Idle;

namespace GeoSlayer.Tests.Services.Idle
{
    /// <summary>
    /// Worker wages (DESIGN.md §5.2, §5D.3).
    ///
    /// <para>§5.2 always said "food, coin". It was read as two currencies for one cost, which
    /// would have made players optimise to whichever was cheaper and ignore the other. It is
    /// now <b>two costs</b>: workers are paid in coin and fed on top, the way employment
    /// works.</para>
    /// </summary>
    [TestFixture]
    public class WorkerWageTests
    {
        [Test]
        public void AnHourOfWork_OwesAnHourOfWages()
        {
            Assert.That(OfflineAccrual.WagesRequired(TimeSpan.FromHours(1)),
                Is.EqualTo((long)OfflineAccrual.CoinPerHour));
        }

        [Test]
        public void WagesRoundUp_SoTheSinkCannotBeDodged()
        {
            // Same reasoning as food: syncing constantly must not make upkeep free.
            Assert.That(OfflineAccrual.WagesRequired(TimeSpan.FromMinutes(1)),
                Is.GreaterThan(0));
        }

        [Test]
        public void NoTimeWorked_OwesNothing()
        {
            Assert.Multiple(() =>
            {
                Assert.That(OfflineAccrual.WagesRequired(TimeSpan.Zero), Is.Zero);
                Assert.That(OfflineAccrual.WagesRequired(TimeSpan.FromHours(-3)), Is.Zero);
            });
        }

        [Test]
        public void AFullOfflineWindow_CostsAFewCoins()
        {
            // The number a player actually meets. At the 4h base cap this must stay small
            // enough not to frighten someone who has just started.
            var wages = OfflineAccrual.WagesRequired(
                TimeSpan.FromHours(OfflineAccrual.BaseOfflineCapHours));

            Assert.That(wages, Is.LessThanOrEqualTo(10));
        }

        // ── The balance that matters (§7.4) ─────────────────────────────

        [Test]
        public void AWorkerStillNetsPositive_EvenAtTierOne()
        {
            // The load-bearing property. A worker that costs more than it produces is a
            // worker nobody runs, and upkeep is meant to be a sink rather than a tax.
            //
            // A tier-1 worker makes ~2 material units an hour. Priced as Mined tier 1, that
            // is worth CoinPricing.UnitPrice(1, Mined) each.
            var producedPerHour =
                OfflineAccrual.BaseMaterialsPerHour
                * CoinPricing.UnitPrice(1, Domain.Enums.MaterialCategory.Mined);

            var wagePerHour = OfflineAccrual.CoinPerHour;

            Assert.That(wagePerHour, Is.LessThan(producedPerHour),
                "a tier-1 worker must still be worth running");
        }

        [Test]
        public void TheWageFadesAsWorkersImprove()
        {
            // Correct by design: the pressure should be early, when a coin matters. A
            // mid-game worker's output dwarfs a flat wage.
            var tierFiveOutput =
                OfflineAccrual.BaseMaterialsPerHour
                * CoinPricing.UnitPrice(5, Domain.Enums.MaterialCategory.Mined);

            Assert.That(OfflineAccrual.CoinPerHour / tierFiveOutput, Is.LessThan(0.05),
                "the wage should be a rounding error by mid-game");
        }

        [Test]
        public void FoodCameDownWhenWagesWentOn()
        {
            // Doubling the total burden would have made workers not worth running. Food was
            // halved as coin was added, so the combined cost stayed in the same range.
            Assert.That(OfflineAccrual.FoodPerHour, Is.LessThan(1.0),
                "food should have been reduced when wages arrived");
        }

        [Test]
        public void FoodAndWages_AreBothRequired()
        {
            // Neither substitutes for the other — that is the entire point of the shape.
            // Two parallel currencies for one cost is what this design avoids.
            var window = TimeSpan.FromHours(4);

            Assert.Multiple(() =>
            {
                Assert.That(OfflineAccrual.FoodRequired(window), Is.GreaterThan(0));
                Assert.That(OfflineAccrual.WagesRequired(window), Is.GreaterThan(0));
            });
        }
    }
}
