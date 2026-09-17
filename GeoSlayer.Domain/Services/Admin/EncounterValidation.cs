using GeoSlayer.Domain.Database.Models;

namespace GeoSlayer.Domain.Services.Admin
{
    /// <summary>
    /// The rules a set of encounter definitions must satisfy (Stage 18 task 8).
    ///
    /// <para><b>One of these is the most load-bearing rule in the game.</b> §5C.2 says
    /// historic POIs are a <i>boost</i>, never the only venue — a player with no castle
    /// within twenty miles must still be able to train Combat to the top. Stage 16 proved it
    /// with <c>EveryTier_IsReachableWithoutHistoricGround</c>, which checks the roaming
    /// ladder covers all seven tiers.</para>
    ///
    /// <para>That test reads seed data. An admin deleting one roaming encounter, or flipping
    /// it to a training ground, would break the rule at runtime while the build stayed green
    /// — and the symptom would be a player quietly unable to progress past a tier, which is
    /// close to undiagnosable from a bug report.</para>
    ///
    /// <para>So the rule is checked <b>against the resulting set</b>, not the single row
    /// being edited. That is why these methods take the whole collection.</para>
    /// </summary>
    public static class EncounterValidation
    {
        /// <summary>Tiers the Combat ladder has, and therefore that encounters must cover.</summary>
        public const int LadderTiers = 7;

        /// <summary>Why this definition cannot be saved on its own terms, or null.</summary>
        public static string? Reject(
            EncounterDefinition candidate,
            IReadOnlyCollection<EncounterDefinition> others)
        {
            if (string.IsNullOrWhiteSpace(candidate.Key))
            {
                return "An encounter needs a key.";
            }

            if (string.IsNullOrWhiteSpace(candidate.Name))
            {
                return "An encounter needs a name.";
            }

            if (others.Any(e => string.Equals(e.Key, candidate.Key, StringComparison.OrdinalIgnoreCase)))
            {
                return $"Another encounter already uses the key '{candidate.Key}'.";
            }

            if (candidate.Tier is < 1 or > LadderTiers)
            {
                return $"Tier must be between 1 and {LadderTiers} — encounters award Combat " +
                       "materials, and the ladder has seven rungs.";
            }

            if (candidate.MinCombatLevel < 1)
            {
                return "Minimum Combat level must be at least 1.";
            }

            // §5C.1: tier 1 sits at level 1 so a new player always has something to find.
            // Gating the bottom rung would mean a level-1 player meets nothing at all.
            if (candidate.Tier == 1 && candidate.MinCombatLevel > 1)
            {
                return "Tier 1 must be reachable at Combat level 1, or a new player meets " +
                       "nothing at all (§5C.1).";
            }

            return null;
        }

        /// <summary>
        /// Why the resulting <i>set</i> would be invalid, or null.
        ///
        /// <para>Checked on save and on delete, because both can break it. This is §5C.2's
        /// geography rule, and it is the one worth refusing over.</para>
        /// </summary>
        public static string? RejectSet(IReadOnlyCollection<EncounterDefinition> resulting)
        {
            var roamingTiers = resulting
                .Where(e => !e.IsTrainingGround)
                .Select(e => e.Tier)
                .Distinct()
                .ToHashSet();

            var missing = Enumerable.Range(1, LadderTiers)
                .Where(tier => !roamingTiers.Contains(tier))
                .ToList();

            if (missing.Count > 0)
            {
                return $"No roaming encounter would cover tier{(missing.Count == 1 ? "" : "s")} " +
                       $"{string.Join(", ", missing)}. A player with no historic ground nearby " +
                       "would be unable to train Combat past that point, which §5C.2 forbids — " +
                       "castles are a boost, never the only venue.";
            }

            return null;
        }

        /// <summary>
        /// Things worth saying that do not justify refusing the save.
        /// </summary>
        public static IReadOnlyList<string> Warn(IReadOnlyCollection<EncounterDefinition> resulting)
        {
            var warnings = new List<string>();

            var trainingTiers = resulting
                .Where(e => e.IsTrainingGround)
                .Select(e => e.Tier)
                .Distinct()
                .ToHashSet();

            if (trainingTiers.Count == 0)
            {
                warnings.Add(
                    "No training grounds at all. Nothing breaks — roaming encounters cover " +
                    "every tier — but historic ground would stop meaning anything.");
            }

            // §5C.1's compensating advantage is availability, not reward. A training ground
            // paying a *higher* tier than roaming at the same level would make a castle a
            // requirement rather than a boost.
            foreach (var ground in resulting.Where(e => e.IsTrainingGround))
            {
                var roamingAtLevel = resulting
                    .Where(e => !e.IsTrainingGround && e.MinCombatLevel <= ground.MinCombatLevel)
                    .Select(e => e.Tier)
                    .DefaultIfEmpty(0)
                    .Max();

                if (ground.Tier > roamingAtLevel)
                {
                    warnings.Add(
                        $"'{ground.Key}' pays tier {ground.Tier} at Combat level " +
                        $"{ground.MinCombatLevel}, but the best roaming encounter at that level " +
                        $"pays tier {roamingAtLevel}. §5C.1 makes historic ground more " +
                        "*available*, not more rewarding.");
                }
            }

            return warnings;
        }
    }
}
