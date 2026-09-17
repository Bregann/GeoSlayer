using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Economy;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Skills;

namespace GeoSlayer.Tests.Services.Economy
{
    /// <summary>
    /// The shape of the coin economy (DESIGN.md §5.4), asserted without a database.
    /// </summary>
    [TestFixture]
    public class CoinPricingTests
    {
        // ── Everything is worth something (§4.1a) ───────────────────────

        [Test]
        public void EverySeededMaterial_HasAPriceOfAtLeastOne()
        {
            // A material that sells for nothing is dead weight a player carries forever —
            // which is exactly what Dust was before this existed.
            var all = MaterialSeedData.Materials.Concat(SkillSeedData.AllSkillMaterials).ToList();

            Assert.That(all, Is.Not.Empty);

            foreach (var material in all)
            {
                var price = CoinPricing.UnitPrice(material.Tier, material.Category);

                Assert.That(price, Is.GreaterThanOrEqualTo(1),
                    $"{material.Key} (tier {material.Tier}, {material.Category}) is worthless");
            }
        }

        [Test]
        public void EveryCategory_IsPricedDeliberately()
        {
            // The fallback exists so a new category is never worth zero, but a category that
            // *reaches* the fallback is one nobody priced. Catching that here is the whole
            // reason the multiplier table is exhaustive.
            var unpriced = Enum.GetValues<MaterialCategory>()
                .Where(c => !CoinPricing.IsPriced(c))
                .ToList();

            Assert.That(unpriced, Is.Empty, $"unpriced categories: {string.Join(", ", unpriced)}");
        }

        [Test]
        public void Dust_IsWorthAboutACoin()
        {
            // Dust's whole job is that walking across featureless ground still pays. If it
            // were worth more it would stop being filler; worth less, and Open terrain pays
            // nothing again.
            Assert.That(CoinPricing.UnitPrice(1, MaterialCategory.Dust), Is.EqualTo(1));
        }

        // ── Advancing must never pay worse (§4.1a) ──────────────────────

        [Test]
        public void PriceRisesWithTier_WithinACategory()
        {
            foreach (var category in Enum.GetValues<MaterialCategory>())
            {
                for (var tier = 2; tier <= 7; tier++)
                {
                    Assert.That(
                        CoinPricing.UnitPrice(tier, category),
                        Is.GreaterThan(CoinPricing.UnitPrice(tier - 1, category)),
                        $"{category} tier {tier} is worth no more than tier {tier - 1}");
                }
            }
        }

        [Test]
        public void HigherTiers_PayMorePerGatherSecond()
        {
            // §4.1a's real requirement: a player is never *punished* for advancing. A tier-7
            // material takes 20× as long to gather as tier 1, so if it were not worth
            // appreciably more, levelling would cut your income.
            var ladder = SkillSeedData.StandardLadder;

            var lowest = ladder.First();
            var highest = ladder.Last();

            var lowRate = CoinPricing.UnitPrice(lowest.Tier, MaterialCategory.Mined) / lowest.GatherSeconds;
            var highRate = CoinPricing.UnitPrice(highest.Tier, MaterialCategory.Mined) / highest.GatherSeconds;

            Assert.That(highRate, Is.GreaterThan(lowRate),
                "coin per gathering second must not fall as tiers rise");
        }

        // ── Crafting must not be a loss (§4.2) ──────────────────────────

        [Test]
        public void ProducedGoods_OutvalueRawOnesOfTheSameTier()
        {
            // A crafted item costs inputs *and* a craft timer. If it sold for the same as its
            // parts, crafting would be a way to lose money and nobody would do it.
            for (var tier = 1; tier <= 7; tier++)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(CoinPricing.UnitPrice(tier, MaterialCategory.Forged),
                        Is.GreaterThan(CoinPricing.UnitPrice(tier, MaterialCategory.Mined)),
                        $"tier {tier} forged goods are worth no more than the ore");

                    Assert.That(CoinPricing.UnitPrice(tier, MaterialCategory.Cooked),
                        Is.GreaterThan(CoinPricing.UnitPrice(tier, MaterialCategory.Foraged)),
                        $"tier {tier} cooked goods are worth no more than raw ingredients");
                });
            }
        }

        // ── The sell bonus ──────────────────────────────────────────────

        [Test]
        public void StackPrice_IsUnitPriceTimesQuantity_WithNoBonus()
        {
            Assert.That(
                CoinPricing.StackPrice(3, MaterialCategory.Mined, 10, 0),
                Is.EqualTo(CoinPricing.UnitPrice(3, MaterialCategory.Mined) * 10));
        }

        [Test]
        public void TheBonus_AppliesToTheWholeStack_NotPerUnit()
        {
            // Per-unit rounding would swallow the entire bonus on cheap materials, which is
            // most of them — a +10% bonus on a 1-coin material would round to nothing.
            var withBonus = CoinPricing.StackPrice(1, MaterialCategory.Dust, 100, 0.10);
            var without = CoinPricing.StackPrice(1, MaterialCategory.Dust, 100, 0);

            Assert.That(withBonus, Is.EqualTo(110));
            Assert.That(without, Is.EqualTo(100));
        }

        [Test]
        public void ANegativeBonus_NeverReducesThePrice()
        {
            Assert.That(
                CoinPricing.StackPrice(2, MaterialCategory.Urban, 10, -0.5),
                Is.EqualTo(CoinPricing.StackPrice(2, MaterialCategory.Urban, 10, 0)));
        }

        [Test]
        public void SellingNothing_EarnsNothing()
        {
            Assert.That(CoinPricing.StackPrice(5, MaterialCategory.Mined, 0, 0.5), Is.Zero);
        }

        // ── The junk shortcut ───────────────────────────────────────────

        [Test]
        public void JunkIsLowTierOnly()
        {
            Assert.Multiple(() =>
            {
                Assert.That(CoinPricing.IsJunk(1, MaterialCategory.Urban), Is.True);
                Assert.That(CoinPricing.IsJunk(2, MaterialCategory.Urban), Is.True);
                Assert.That(CoinPricing.IsJunk(3, MaterialCategory.Urban), Is.False,
                    "the shortcut must not reach anything a player might be saving");
            });
        }

        [Test]
        public void ARelicIsNeverJunk_WhateverItsTier()
        {
            // Relics are Museum pieces. Bulk-selling one by accident is the precise annoyance
            // the shortcut's caution exists to prevent.
            for (var tier = 1; tier <= 7; tier++)
            {
                Assert.That(CoinPricing.IsJunk(tier, MaterialCategory.Relic), Is.False,
                    $"a tier {tier} Relic was treated as junk");
            }
        }
    }
}
