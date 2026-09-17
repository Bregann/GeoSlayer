using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Admin
{
    /// <summary>
    /// Which system reads each <see cref="ItemModifier"/> (Stage 18 task 4).
    ///
    /// <para>§4.3 is explicit: "each value must be <b>read by the system it names</b>… adding
    /// a value here without a reader is worse than not adding it." That rule has held so far
    /// because the enum is small enough to audit by hand. An admin interface that lets
    /// someone pick a modifier from a dropdown removes that safety — so the rule needs
    /// stating somewhere a machine can check it.</para>
    ///
    /// <para><b>This is a claim, and a test verifies it.</b> Every value must appear here, and
    /// the named reader must actually exist in the codebase. That test is the point of the
    /// class: without it this would just be a second place to forget to update.</para>
    /// </summary>
    public static class ModifierReaders
    {
        /// <summary>
        /// Modifier → the type that reads it, and how.
        ///
        /// <para>The description is written for an admin choosing a modifier, not for a
        /// developer — "what will this actually do if I put it on an item" is the question
        /// being answered.</para>
        /// </summary>
        public static IReadOnlyDictionary<ItemModifier, (string Reader, string Effect)> All { get; } =
            new Dictionary<ItemModifier, (string, string)>
            {
                [ItemModifier.SkillXpPercent] =
                    ("ProgressionService", "Raises XP from every source, by this fraction."),

                [ItemModifier.RevealRadius] =
                    ("FogService", "Widens the corridor each step reveals, in cells."),

                [ItemModifier.PoiRangeMetres] =
                    ("SkillTrainingService, EconomyService",
                     "Extends how far away a POI can be visited, trained at, or traded with."),

                [ItemModifier.OfflineCapHours] =
                    ("WorkerService, EconomyService",
                     "Extends both offline worker accrual and banking interest."),

                [ItemModifier.SellPricePercent] =
                    ("MaterialService.SellPriceBonus", "Raises what materials fetch at a shop."),

                [ItemModifier.WorkerRatePercent] =
                    ("WorkerService", "Raises what offline workers produce."),

                [ItemModifier.ToolTier] =
                    ("MaterialService, DropRoller", "Caps the highest tier this tool can gather."),

                [ItemModifier.GatherSpeedPercent] =
                    ("MaterialService", "More units gathered per cell within a tier."),

                [ItemModifier.CombatPowerLevels] =
                    ("EncounterService, EncounterResolution",
                     "Fights as though the player's Combat level were this much higher."),
            };

        /// <summary>Whether a modifier has a declared reader.</summary>
        public static bool IsRead(ItemModifier modifier) => All.ContainsKey(modifier);

        /// <summary>What a modifier does, for the admin interface.</summary>
        public static string EffectOf(ItemModifier modifier) =>
            All.TryGetValue(modifier, out var entry)
                ? entry.Effect
                : "Nothing reads this modifier — an item using it will have no effect (§4.3).";
    }
}
