using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Economy
{
    /// <summary>
    /// What a material is worth in coin (DESIGN.md §5.4, resolving §9.5).
    ///
    /// <para><b>Derived, never enumerated.</b> There are over a hundred materials across
    /// fifteen ladders, and a hand-written price list would rot the moment a ladder is added
    /// — the same content-debt trap §5B.2 rejects for clue text. Price comes from tier and
    /// category, so a new material is priced correctly the day it is seeded.</para>
    ///
    /// <para>Pure and static, so the whole economy's shape can be asserted without a
    /// database.</para>
    /// </summary>
    public static class CoinPricing
    {
        /// <summary>
        /// Coin for a tier-1 material of an ordinary category.
        ///
        /// <para>Deliberately 1: Dust is tier 1 and its whole job is that a walk across
        /// featureless ground still pays <i>something</i>. A floor of zero would make the
        /// thin-geography player's haul literally worthless, which §4.1a forbids.</para>
        /// </summary>
        public const long BaseValue = 1;

        /// <summary>
        /// How steeply value rises per tier.
        ///
        /// <para>Tier already costs more time to gather (<c>BaseGatherSeconds</c> runs
        /// 3→60s across the ladder, a 20× spread). Pricing at roughly <c>tier²</c> keeps
        /// coin-per-hour rising with tier rather than falling, so a player is never
        /// <i>paid less</i> for advancing — the same rule §4.1a applies to XP.</para>
        /// </summary>
        public static long TierValue(int tier)
        {
            var t = Math.Max(1, tier);

            return t * t;
        }

        /// <summary>
        /// Per-category multiplier, in percent of the base price.
        ///
        /// <para>Categories are worth different amounts because they cost different amounts
        /// to reach. Terrain pools (Woodland, Rocky…) are what you walk over; skill ladders
        /// are what you trained for; Relics come from the rarest POIs. Anything unlisted
        /// falls back to <see cref="DefaultMultiplier"/>, so a new category is priced
        /// sanely rather than at zero.</para>
        /// </summary>
        private static readonly Dictionary<MaterialCategory, int> Multipliers = new()
        {
            // Filler. Worth almost nothing per unit, which is correct — its value is that it
            // is constant, not that it is good.
            [MaterialCategory.Dust] = 100,

            // Terrain pools: gathered by walking, so the cheapest real materials.
            [MaterialCategory.Woodland] = 120,
            [MaterialCategory.Water] = 120,
            [MaterialCategory.Farmland] = 120,
            [MaterialCategory.Urban] = 120,
            [MaterialCategory.Industrial] = 140,
            [MaterialCategory.Rocky] = 140,
            [MaterialCategory.Coastal] = 140,

            // Gathering ladders: need the skill trained to reach the higher tiers.
            [MaterialCategory.Foraged] = 150,
            [MaterialCategory.Caught] = 150,
            [MaterialCategory.Logged] = 150,
            [MaterialCategory.Mined] = 160,
            [MaterialCategory.Grown] = 150,
            [MaterialCategory.Martial] = 170,

            // Produced goods: cost inputs *and* a craft, so they must beat their parts or
            // crafting is a loss-making exercise and nobody will ever do it.
            [MaterialCategory.Cooked] = 220,
            [MaterialCategory.Forged] = 240,

            // POI-gated ladders: gathered by visiting specific places rather than by walking.
            [MaterialCategory.Traded] = 180,
            [MaterialCategory.Sacred] = 170,
            [MaterialCategory.Written] = 170,
            [MaterialCategory.Remedy] = 180,
            [MaterialCategory.Athletic] = 170,
            [MaterialCategory.Brewed] = 180,

            // Banking's ladder — valuables, the best thing to sell, which is what makes a
            // bank worth visiting even before deposits (§5.4a).
            [MaterialCategory.Coin] = 260,

            // Relics are Museum pieces. Priced high so selling one is a real decision, and
            // the Museum's own framing (§5A.1) gives the reason not to.
            [MaterialCategory.Relic] = 400,
        };

        /// <summary>
        /// Fallback for a category added without a price.
        ///
        /// <para>Chosen to be sane rather than safe: a new ladder is most likely a gathering
        /// one, so it is priced like one. <c>IsPriced</c> is what catches the omission — the
        /// fallback exists so a forgotten category is never <i>worthless</i>, not so it can
        /// be forgotten.</para>
        /// </summary>
        public const int DefaultMultiplier = 150;

        /// <summary>Whether a category was priced deliberately, rather than falling back.</summary>
        public static bool IsPriced(MaterialCategory category) => Multipliers.ContainsKey(category);

        /// <summary>The multiplier for a category, in percent.</summary>
        public static int MultiplierFor(MaterialCategory category) =>
            Multipliers.GetValueOrDefault(category, DefaultMultiplier);

        /// <summary>
        /// What one unit sells for, before any player bonuses.
        ///
        /// <para>Floored at 1. A material that sells for nothing is one the player carries
        /// forever for no reason, which is the exact dead weight Dust used to be.</para>
        /// </summary>
        public static long UnitPrice(int tier, MaterialCategory category)
        {
            var raw = BaseValue * TierValue(tier) * MultiplierFor(category) / 100;

            return Math.Max(1, raw);
        }

        /// <summary>
        /// What a stack sells for, with the player's sell-price bonus applied.
        /// </summary>
        /// <param name="bonus">
        /// Fractional bonus from Storehouse, the Museum and Banking level — 0.25 for +25%.
        /// </param>
        public static long StackPrice(int tier, MaterialCategory category, long quantity, double bonus)
        {
            if (quantity <= 0)
            {
                return 0;
            }

            var unit = UnitPrice(tier, category);
            var multiplier = 1 + Math.Max(0, bonus);

            // Bonus applied to the total rather than per unit: rounding a per-unit price down
            // would silently swallow the whole bonus on cheap materials, which is most of
            // them.
            return (long)Math.Floor(unit * quantity * multiplier);
        }

        /// <summary>
        /// The tier at or below which a material counts as junk, for "sell all junk".
        ///
        /// <para>Exists so the common case is one tap without risking the ore someone was
        /// saving for a recipe. Deliberately low: the shortcut should be obviously safe, and
        /// anything a player might plausibly be hoarding is left for them to choose.</para>
        /// </summary>
        public const int JunkTierThreshold = 2;

        /// <summary>
        /// Whether a material is safe for the bulk-sell shortcut.
        ///
        /// <para>Relics are never junk regardless of tier — they are Museum pieces, and
        /// bulk-selling one by accident is exactly the annoyance this guard exists for.</para>
        /// </summary>
        public static bool IsJunk(int tier, MaterialCategory category) =>
            category != MaterialCategory.Relic && tier <= JunkTierThreshold;
    }
}
