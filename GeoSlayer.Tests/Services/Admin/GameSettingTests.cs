using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Admin;
using GeoSlayer.Domain.Services.Economy;

namespace GeoSlayer.Tests.Services.Admin
{
    /// <summary>
    /// The tunable-numbers table (Stage 18).
    ///
    /// <para>The property that matters most: <b>seeding this table changes no behaviour</b>.
    /// Every default is the value the constant already held, so a deployment that adds the
    /// table plays identically to one without it. A migration that also retunes the game
    /// would be two changes wearing one hat.</para>
    /// </summary>
    [TestFixture]
    public class GameSettingTests
    {
        [Test]
        public void EverySettingHasADefaultEqualToItsValue()
        {
            // Seeded rows start unchanged by definition — Value and Default diverge only
            // when an admin edits one.
            foreach (var setting in GameSettingKeys.All)
            {
                Assert.That(setting.Value, Is.EqualTo(setting.Default), $"{setting.Key} differs");
            }
        }

        [Test]
        public void EveryDefaultIsANumberWithinItsOwnBounds()
        {
            // A shipped default outside its own range would make the setting unsaveable
            // without first changing it, which is a trap rather than a guard.
            foreach (var setting in GameSettingKeys.All)
            {
                Assert.That(double.TryParse(setting.Value, out var value), Is.True,
                    $"{setting.Key} has an unparseable default '{setting.Value}'");

                Assert.That(value, Is.InRange(setting.MinValue, setting.MaxValue),
                    $"{setting.Key} default {value} is outside [{setting.MinValue}, {setting.MaxValue}]");
            }
        }

        [Test]
        public void EverySettingHasABoundedRange()
        {
            // An unbounded tuning value is a way to break the game from a text box.
            foreach (var setting in GameSettingKeys.All)
            {
                Assert.That(setting.MaxValue, Is.GreaterThan(setting.MinValue),
                    $"{setting.Key} has an empty or inverted range");
            }
        }

        [Test]
        public void EverySettingIsDescribedAndCategorised()
        {
            // The reader is someone deciding whether to change it. A blank description is a
            // number nobody can safely touch.
            foreach (var setting in GameSettingKeys.All)
            {
                Assert.That(setting.Description, Is.Not.Empty, $"{setting.Key} has no description");
                Assert.That(setting.Category, Is.Not.Empty, $"{setting.Key} has no category");
            }
        }

        [Test]
        public void KeysAreUnique()
        {
            // The table has a unique index; a duplicate here would fail seeding at boot.
            var keys = GameSettingKeys.All.Select(s => s.Key).ToList();

            Assert.That(keys, Is.Unique);
        }

        [Test]
        public void EveryMaterialCategoryHasAPriceSetting()
        {
            // Otherwise a category is silently untunable — the exact gap this table exists
            // to close.
            var keys = GameSettingKeys.All.Select(s => s.Key).ToHashSet();

            foreach (var category in Enum.GetValues<MaterialCategory>())
            {
                Assert.That(keys, Does.Contain(GameSettingKeys.CoinCategoryMultiplier(category)),
                    $"{category} has no price setting");
            }
        }

        [Test]
        public void TheSeededMultipliersMatchTheShippedPricing()
        {
            // The no-op property, checked directly: seeding the table must reproduce exactly
            // what CoinPricing already returns.
            foreach (var category in Enum.GetValues<MaterialCategory>())
            {
                var key = GameSettingKeys.CoinCategoryMultiplier(category);
                var seeded = GameSettingKeys.All.First(s => s.Key == key);

                Assert.That(int.Parse(seeded.Value), Is.EqualTo(CoinPricing.MultiplierFor(category)),
                    $"{category} would be repriced by seeding");
            }
        }

        [Test]
        public void TheSeededRatesMatchTheShippedConstants()
        {
            var byKey = GameSettingKeys.All.ToDictionary(s => s.Key, s => double.Parse(s.Value));

            Assert.Multiple(() =>
            {
                Assert.That(byKey[GameSettingKeys.BankingInterestRate],
                    Is.EqualTo(BankingInterest.RatePerHour));

                Assert.That(byKey[GameSettingKeys.DistanceSynergyXpPerKm],
                    Is.EqualTo(Domain.Services.Skills.SkillSeedData.DistanceSynergyXpPerKilometre));

                Assert.That(byKey[GameSettingKeys.SellPricePerSkillLevel],
                    Is.EqualTo(Domain.Services.Skills.SkillSeedData.SellPricePerSkillLevel));

                Assert.That(byKey[GameSettingKeys.CoinBaseValue],
                    Is.EqualTo(CoinPricing.BaseValue));

                Assert.That(byKey[GameSettingKeys.JunkTierThreshold],
                    Is.EqualTo(CoinPricing.JunkTierThreshold));
            });
        }

        [Test]
        public void WithNoSettingsWiredUp_PricingUsesTheShippedDefaults()
        {
            // CoinPricing.Settings is null in tests and before the API boots. The formula
            // must behave identically either way, or every pricing test would depend on
            // whether a table happened to be loaded.
            Assert.That(CoinPricing.Settings, Is.Null, "a previous test leaked a settings source");

            Assert.That(CoinPricing.UnitPrice(1, MaterialCategory.Dust), Is.EqualTo(1));
            Assert.That(CoinPricing.IsJunk(2, MaterialCategory.Urban), Is.True);
            Assert.That(CoinPricing.IsJunk(3, MaterialCategory.Urban), Is.False);
        }
    }
}
