using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Combat
{
    /// <summary>
    /// Seeded encounter definitions (DESIGN.md §5C.1).
    ///
    /// <para>Two parallel ladders over the same seven Combat tiers. They award the same
    /// materials at the same levels — the difference is <b>availability</b>, not reward:
    /// roaming encounters expire and training grounds do not.</para>
    ///
    /// <para>That is deliberate and is what keeps §5C.2 honest. If training grounds paid
    /// better, a player with no castle would be permanently behind rather than merely
    /// slower, which is the geography lockout every skill in this game is built to
    /// avoid.</para>
    /// </summary>
    public static class EncounterSeedData
    {
        /// <summary>
        /// The skill encounters train.
        ///
        /// <para>Declared here rather than compared for inside the service: encounters are
        /// a Combat-only system, so this is configuration, not a per-skill branch. Keeping
        /// it in seed data is what keeps the Stage 04 rule honest — one place names the
        /// skill, and no service code decides anything by asking which skill it has.</para>
        /// </summary>
        public static readonly SkillType Skill = SkillType.Combat;

        /// <summary>The Combat ladder's level gates, shared with <c>SkillSeedData</c>.</summary>
        private static readonly int[] TierLevels = [1, 10, 20, 35, 50, 70, 90];

        private static readonly string[] RoamingNames =
        [
            "Stray Dog", "Cutpurse", "Highwayman", "Deserter",
            "Mercenary Band", "Knight Errant", "Champion",
        ];

        private static readonly string[] TrainingNames =
        [
            "Practice Post", "Drill Yard", "Garrison Watch", "Armoury Guard",
            "Castellan's Guard", "Old Guard", "Warden of the Keep",
        ];

        public static IReadOnlyList<EncounterDefinition> Encounters { get; } = Build();

        private static List<EncounterDefinition> Build()
        {
            var all = new List<EncounterDefinition>();

            for (var tier = 0; tier < TierLevels.Length; tier++)
            {
                all.Add(new EncounterDefinition
                {
                    Key = $"roaming_{tier + 1}",
                    Name = RoamingNames[tier],
                    Description = $"A {RoamingNames[tier].ToLowerInvariant()} crosses your path.",
                    MinCombatLevel = TierLevels[tier],
                    Tier = tier + 1,
                    IsTrainingGround = false,
                });

                all.Add(new EncounterDefinition
                {
                    Key = $"training_{tier + 1}",
                    Name = TrainingNames[tier],
                    Description = $"The {TrainingNames[tier].ToLowerInvariant()} will spar with you.",
                    MinCombatLevel = TierLevels[tier],
                    Tier = tier + 1,
                    IsTrainingGround = true,
                });
            }

            return all;
        }
    }
}
