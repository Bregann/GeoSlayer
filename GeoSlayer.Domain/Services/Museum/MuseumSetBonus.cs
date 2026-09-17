using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Museum
{
    /// <summary>
    /// Permanent bonuses for completing a Museum wing (DESIGN.md §5A, Stage 12 task 3).
    ///
    /// <para><b>Deliberately small.</b> The task says "sparingly… the Museum should be pursued
    /// for its own sake, not because it is mandatory". A wing is dozens of finds; if the bonus
    /// were large, filling it would stop being a choice.</para>
    ///
    /// <para>Deferred from Stage 12 to Stage 14 because Relics and Expeditions had no entries
    /// until Stages 13–14 — a bonus balanced against an unfinishable Museum would have been
    /// balanced against nothing.</para>
    /// </summary>
    public static class MuseumSetBonus
    {
        /// <summary>What completing a wing grants.</summary>
        public readonly record struct Bonus(ItemModifier Modifier, double Value, string Description);

        /// <summary>
        /// Each wing's bonus, themed to what filling it required.
        ///
        /// The Cartography bonus is absent on purpose: that wing has no fixed size — regions
        /// are created on discovery — so it can never be "complete", and promising a bonus
        /// for finishing it would be a lie.
        /// </summary>
        public static IReadOnlyDictionary<MuseumWing, Bonus> Bonuses { get; } =
            new Dictionary<MuseumWing, Bonus>
            {
                [MuseumWing.Landmarks] = new(
                    ItemModifier.PoiRangeMetres, 10,
                    "+10m POI range — you know how these places sit."),

                [MuseumWing.Naturalist] = new(
                    ItemModifier.SkillXpPercent, 0.03,
                    "+3% skill XP — you read ground well."),

                [MuseumWing.Skills] = new(
                    ItemModifier.StackCapPercent, 0.10,
                    "+10% stack caps — you know what is worth keeping."),

                [MuseumWing.Rarities] = new(
                    ItemModifier.SkillXpPercent, 0.02,
                    "+2% skill XP."),

                [MuseumWing.Feats] = new(
                    ItemModifier.OfflineCapHours, 1,
                    "+1h offline cap."),

                [MuseumWing.Relics] = new(
                    ItemModifier.WorkerRatePercent, 0.05,
                    "+5% worker output."),

                [MuseumWing.Expeditions] = new(
                    ItemModifier.WorkerRatePercent, 0.05,
                    "+5% worker output."),
            };

        /// <summary>
        /// Total value of a modifier across every completed wing.
        /// </summary>
        public static double TotalFor(ItemModifier modifier, IEnumerable<MuseumWing> completedWings) =>
            completedWings
                .Where(w => Bonuses.ContainsKey(w))
                .Select(w => Bonuses[w])
                .Where(b => b.Modifier == modifier)
                .Sum(b => b.Value);
    }
}
