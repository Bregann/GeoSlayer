using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Admin
{
    /// <summary>
    /// Every tunable number, and what it was shipped as (Stage 18).
    ///
    /// <para>One place naming them, for the same reason <c>EncounterSeedData.Skill</c> and
    /// <c>SkillSeedData.SellPriceSkills</c> exist: a key that is a string literal at both the
    /// read and the write is a key that will eventually be misspelled at one of them, and a
    /// misspelled setting silently falls back to its default forever.</para>
    ///
    /// <para><b>Bounds are part of the definition.</b> A tuning value with no ceiling is a
    /// way to break the game from a text box — an interest rate of 10 per hour, or a coin
    /// multiplier of zero that makes every material worthless. The ranges here are wide
    /// enough to experiment in and narrow enough to stay a game.</para>
    /// </summary>
    public static class GameSettingKeys
    {
        // ── Economy ─────────────────────────────────────────────────────

        /// <summary>Coin for a tier-1 material of an ordinary category.</summary>
        public const string CoinBaseValue = "economy.coin.base_value";

        /// <summary>Per-category price multiplier, in percent. Suffixed with the category.</summary>
        public static string CoinCategoryMultiplier(MaterialCategory category) =>
            $"economy.coin.multiplier.{category.ToString().ToLowerInvariant()}";

        /// <summary>Tier at or below which a material counts as junk for the bulk-sell.</summary>
        public const string JunkTierThreshold = "economy.coin.junk_tier";

        /// <summary>Interest per hour on a deposited balance, as a fraction.</summary>
        public const string BankingInterestRate = "economy.banking.interest_per_hour";

        // ── Skills ──────────────────────────────────────────────────────

        /// <summary>XP per kilometre walked, for the distance-synergy skills.</summary>
        public const string DistanceSynergyXpPerKm = "skills.distance_synergy.xp_per_km";

        /// <summary>Sell-price bonus per level in a sell-price skill.</summary>
        public const string SellPricePerSkillLevel = "skills.sell_price.per_level";

        /// <summary>
        /// Every setting, with its shipped default and bounds.
        ///
        /// <para>The defaults are the values these constants held before they moved here, so
        /// seeding this table changes no behaviour — that is deliberate. A migration that
        /// also retunes the game is two changes wearing one hat.</para>
        /// </summary>
        public static IReadOnlyList<GameSetting> All { get; } = BuildAll();

        private static List<GameSetting> BuildAll()
        {
            var settings = new List<GameSetting>
            {
                Make(CoinBaseValue, "1", "Economy",
                    "Coin for a tier-1 material of an ordinary category. Dust is tier 1, and "
                    + "its whole job is that a walk across featureless ground still pays "
                    + "something — setting this to 0 makes a thin-geography player's haul "
                    + "literally worthless.",
                    min: 1, max: 100),

                Make(JunkTierThreshold, "2", "Economy",
                    "Tier at or below which 'sell all junk' will take a material. Deliberately "
                    + "low: the shortcut should be obviously safe, and anything a player might "
                    + "plausibly be hoarding is left for them to choose. Relics are never junk "
                    + "whatever this says.",
                    min: 0, max: 7),

                Make(BankingInterestRate, "0.001", "Economy",
                    "Interest per hour on deposited coin, as a fraction. 0.001 is 0.1%/hour. "
                    + "Deliberately tiny — a game about walking outside must never make sitting "
                    + "still the efficient play. Bounded by the offline cap regardless, so a "
                    + "month away pays what one window pays.",
                    min: 0, max: 0.05),

                Make(DistanceSynergyXpPerKm, "12", "Skills",
                    "XP per kilometre walked, for Athletics. A brisk hour covers ~5km, so this "
                    + "pays ~60 XP against the several hundred the same hour earns from cells — "
                    + "a supplement that makes re-walked ground worth something, not a route "
                    + "around exploring.",
                    min: 0, max: 500),

                Make(SellPricePerSkillLevel, "0.005", "Skills",
                    "Sell-price bonus per Banking level, as a fraction. 0.005 means level 99 "
                    + "sells at +49.5%, comparable to the Counting House.",
                    min: 0, max: 0.05),
            };

            // One row per category rather than a single blob, so an admin can retune Relics
            // without touching Dust and the audit trail records which one moved.
            foreach (var category in Enum.GetValues<MaterialCategory>())
            {
                settings.Add(Make(
                    CoinCategoryMultiplier(category),
                    Economy.CoinPricing.MultiplierFor(category).ToString(),
                    "Economy · category prices",
                    $"Price multiplier for {category} materials, in percent of the base value. "
                    + "100 means base price; 240 means 2.4x. Produced goods are priced above "
                    + "their inputs on purpose — if a crafted item sold for the same as its "
                    + "parts, crafting would lose money and nobody would do it.",
                    min: 1, max: 2000));
            }

            return settings;
        }

        private static GameSetting Make(
            string key, string value, string category, string description, double min, double max) =>
            new()
            {
                Key = key,
                Value = value,
                Default = value,
                Category = category,
                Description = description,
                MinValue = min,
                MaxValue = max,
            };
    }
}
