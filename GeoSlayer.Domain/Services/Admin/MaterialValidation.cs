using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Admin
{
    /// <summary>
    /// The invariants a material must satisfy (Stage 18 task 6).
    ///
    /// <para><b>These rules already exist — as tests.</b> `SkillLaddersDoNotShareAMaterialCategory`,
    /// `NoSkillHasTwoMaterialsAtTheSameTier`, `HigherTiers_RequireHigherLevels` and their
    /// neighbours have guarded the seed data since Stage 04. But a test only runs in CI, and
    /// an admin editing a material at runtime would sail straight past all of them — the
    /// build stays green while the database goes wrong.</para>
    ///
    /// <para>So the rules are restated here, where a save can be refused with a reason. The
    /// duplication is deliberate and the alternative was worse: either the interface could
    /// corrupt the invariants, or the tests would have to be rewritten to load from the
    /// database, which would make them slow and stop them describing the <i>intent</i> of
    /// the seed data.</para>
    ///
    /// <para>Pure and static, so every rule can be asserted without a database.</para>
    /// </summary>
    public static class MaterialValidation
    {
        /// <summary>
        /// Why this material cannot be saved, or null when it is fine.
        /// </summary>
        /// <param name="candidate">The material as it would be saved.</param>
        /// <param name="others">Every other material, for the cross-cutting rules.</param>
        public static string? Reject(Material candidate, IReadOnlyCollection<Material> others)
        {
            if (string.IsNullOrWhiteSpace(candidate.Key))
            {
                return "A material needs a key.";
            }

            if (string.IsNullOrWhiteSpace(candidate.Name))
            {
                return "A material needs a name.";
            }

            // MaterialKeys_AreUnique. The key is what every recipe, drop table and seed file
            // references, so a collision silently repoints them.
            if (others.Any(m => string.Equals(m.Key, candidate.Key, StringComparison.OrdinalIgnoreCase)))
            {
                return $"Another material already uses the key '{candidate.Key}'.";
            }

            if (candidate.Tier is < 1 or > 7)
            {
                return "Tier must be between 1 and 7 — the ladder has seven rungs (§4.1a).";
            }

            if (candidate.LevelRequired < 1)
            {
                return "Level required must be at least 1.";
            }

            if (candidate.BaseGatherSeconds <= 0)
            {
                return "Gather seconds must be above zero, or the material is free.";
            }

            if (candidate.XpPerUnit < 0)
            {
                return "XP per unit cannot be negative.";
            }

            if (candidate.SkillType is null)
            {
                // Universal materials (Dust) sit outside the ladders entirely, so the
                // per-skill rules below do not apply to them.
                return null;
            }

            var skill = candidate.SkillType.Value;
            var ladder = others.Where(m => m.SkillType == skill).ToList();

            // NoSkillHasTwoMaterialsAtTheSameTier. Two materials on one rung makes the
            // "highest unlocked tier wins" roll pick between them arbitrarily.
            if (ladder.Any(m => m.Tier == candidate.Tier))
            {
                return $"{skill} already has a tier {candidate.Tier} material. " +
                       "Two materials on one rung would make drops arbitrary.";
            }

            // SkillLaddersDoNotShareAMaterialCategory. Same failure, one level up: two
            // ladders in a category compete for the same tier band.
            var categoryOwner = others
                .FirstOrDefault(m => m.Category == candidate.Category
                                  && m.SkillType is not null
                                  && m.SkillType != skill);

            if (categoryOwner is not null)
            {
                return $"Category {candidate.Category} already belongs to " +
                       $"{categoryOwner.SkillType}. Each skill ladder needs its own category.";
            }

            // HigherTiers_RequireHigherLevels and HigherTiers_TakeLongerToGather. A ladder
            // that goes backwards means advancing makes you worse off, which §4.1a forbids.
            var below = ladder.Where(m => m.Tier < candidate.Tier).OrderBy(m => m.Tier).LastOrDefault();
            var above = ladder.Where(m => m.Tier > candidate.Tier).OrderBy(m => m.Tier).FirstOrDefault();

            if (below is not null)
            {
                if (candidate.LevelRequired <= below.LevelRequired)
                {
                    return $"Tier {candidate.Tier} must need a higher level than tier " +
                           $"{below.Tier} ({below.LevelRequired}).";
                }

                if (candidate.BaseGatherSeconds <= below.BaseGatherSeconds)
                {
                    return $"Tier {candidate.Tier} must take longer to gather than tier " +
                           $"{below.Tier} ({below.BaseGatherSeconds}s).";
                }
            }

            if (above is not null)
            {
                if (candidate.LevelRequired >= above.LevelRequired)
                {
                    return $"Tier {candidate.Tier} must need a lower level than tier " +
                           $"{above.Tier} ({above.LevelRequired}).";
                }

                if (candidate.BaseGatherSeconds >= above.BaseGatherSeconds)
                {
                    return $"Tier {candidate.Tier} must gather faster than tier " +
                           $"{above.Tier} ({above.BaseGatherSeconds}s).";
                }
            }

            // XpPerHour_NeverFallsAsTiersRise. XP is held proportional to gather time so
            // XP/hour rises across the ladder — a player must never be paid *less* per hour
            // for having advanced.
            if (below is not null && below.BaseGatherSeconds > 0 && candidate.BaseGatherSeconds > 0)
            {
                var lowerRate = below.XpPerUnit / below.BaseGatherSeconds;
                var thisRate = candidate.XpPerUnit / candidate.BaseGatherSeconds;

                if (thisRate < lowerRate)
                {
                    return $"Tier {candidate.Tier} would pay less XP per hour than tier " +
                           $"{below.Tier}, so advancing would make a player worse off (§4.1a).";
                }
            }

            return null;
        }

        /// <summary>
        /// Whether a material may appear in a terrain drop table.
        ///
        /// <para>ProductionMaterials_NeverAppearInDropTables: a cooked pie must not be found
        /// lying in a field. Production skills consume rather than gather, so their output
        /// has no business in a terrain roll.</para>
        /// </summary>
        public static string? RejectAsDrop(Material material, IReadOnlySet<SkillType> productionSkills)
        {
            if (material.SkillType is not null && productionSkills.Contains(material.SkillType.Value))
            {
                return $"{material.Name} is produced by {material.SkillType}, not gathered — " +
                       "a crafted good must not be found lying on the ground.";
            }

            return null;
        }
    }
}
