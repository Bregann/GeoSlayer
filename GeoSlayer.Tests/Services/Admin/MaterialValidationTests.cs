using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Admin;
using GeoSlayer.Domain.Services.Skills;

namespace GeoSlayer.Tests.Services.Admin
{
    /// <summary>
    /// Stage 18 criterion 7: the admin interface must not be able to save a state the test
    /// suite would reject.
    ///
    /// <para>These rules exist twice — as assertions over the seed data, and as this
    /// validator. The duplication is the point: a test only runs in CI, so an admin editing
    /// at runtime would sail past every one of them while the build stayed green.</para>
    /// </summary>
    [TestFixture]
    public class MaterialValidationTests
    {
        private static Material Make(
            string key = "new_material",
            SkillType? skill = SkillType.Mining,
            MaterialCategory category = MaterialCategory.Mined,
            int tier = 3,
            int level = 20,
            double seconds = 9,
            double xp = 25) => new()
            {
                Key = key,
                Name = "New Material",
                SkillType = skill,
                Category = category,
                Tier = tier,
                LevelRequired = level,
                BaseGatherSeconds = seconds,
                XpPerUnit = xp,
            };

        /// <summary>A believable two-rung Mining ladder to validate against.</summary>
        private static List<Material> ExistingLadder() =>
        [
            Make("ore_tin", SkillType.Mining, MaterialCategory.Mined, 1, 1, 3, 5),
            Make("ore_copper", SkillType.Mining, MaterialCategory.Mined, 2, 10, 5, 12),
            Make("ore_gold", SkillType.Mining, MaterialCategory.Mined, 5, 50, 24, 85),
        ];

        // ── The happy path ──────────────────────────────────────────────

        [Test]
        public void AWellFormedMaterial_IsAccepted()
        {
            Assert.That(MaterialValidation.Reject(Make(), ExistingLadder()), Is.Null);
        }

        [Test]
        public void AUniversalMaterial_SkipsTheLadderRules()
        {
            // Dust has no skill, so the per-ladder rules do not apply to it — it sits
            // outside the tier system entirely.
            var dust = Make("dust", skill: null, category: MaterialCategory.Dust, tier: 1, level: 1, seconds: 1, xp: 0);

            Assert.That(MaterialValidation.Reject(dust, ExistingLadder()), Is.Null);
        }

        // ── Identity ────────────────────────────────────────────────────

        [Test]
        public void ADuplicateKey_IsRejected()
        {
            // The key is what every recipe and drop table references, so a collision
            // silently repoints them.
            var clash = Make("ore_copper", tier: 3);

            Assert.That(MaterialValidation.Reject(clash, ExistingLadder()), Is.Not.Null);
        }

        [Test]
        public void ADuplicateKeyIsCaught_RegardlessOfCase()
        {
            Assert.That(MaterialValidation.Reject(Make("ORE_COPPER", tier: 3), ExistingLadder()), Is.Not.Null);
        }

        [Test]
        public void AMissingKeyOrName_IsRejected()
        {
            var noKey = Make();
            noKey.Key = "  ";

            var noName = Make();
            noName.Name = "";

            Assert.Multiple(() =>
            {
                Assert.That(MaterialValidation.Reject(noKey, ExistingLadder()), Is.Not.Null);
                Assert.That(MaterialValidation.Reject(noName, ExistingLadder()), Is.Not.Null);
            });
        }

        // ── The ladder rules ────────────────────────────────────────────

        [Test]
        public void TwoMaterialsOnOneRung_AreRejected()
        {
            // NoSkillHasTwoMaterialsAtTheSameTier — the "highest unlocked tier wins" roll
            // would pick between them arbitrarily.
            Assert.That(MaterialValidation.Reject(Make(tier: 2, level: 10, seconds: 5), ExistingLadder()),
                Is.Not.Null);
        }

        [Test]
        public void TwoLaddersSharingACategory_AreRejected()
        {
            // SkillLaddersDoNotShareAMaterialCategory — same failure one level up.
            var intruder = Make("timber", SkillType.Woodcutting, MaterialCategory.Mined, tier: 3);

            var rejection = MaterialValidation.Reject(intruder, ExistingLadder());

            Assert.That(rejection, Is.Not.Null);
            Assert.That(rejection, Does.Contain("Mining"), "the message should name the owner");
        }

        [Test]
        public void ATierOutsideTheLadder_IsRejected()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MaterialValidation.Reject(Make(tier: 0), ExistingLadder()), Is.Not.Null);
                Assert.That(MaterialValidation.Reject(Make(tier: 8), ExistingLadder()), Is.Not.Null);
            });
        }

        // ── Advancing must never make you worse off (§4.1a) ─────────────

        [Test]
        public void ATierNeedingALowerLevelThanTheOneBelow_IsRejected()
        {
            // HigherTiers_RequireHigherLevels. Tier 3 at level 5 sits below tier 2's level 10.
            Assert.That(MaterialValidation.Reject(Make(tier: 3, level: 5, seconds: 9), ExistingLadder()),
                Is.Not.Null);
        }

        [Test]
        public void ATierNeedingAHigherLevelThanTheOneAbove_IsRejected()
        {
            // The same rule from the other side: tier 3 at level 99 would sit above tier 5.
            Assert.That(MaterialValidation.Reject(Make(tier: 3, level: 99, seconds: 9), ExistingLadder()),
                Is.Not.Null);
        }

        [Test]
        public void ATierGatheringFasterThanTheOneBelow_IsRejected()
        {
            // HigherTiers_TakeLongerToGather. Rarer must mean slower, or the ladder inverts.
            Assert.That(MaterialValidation.Reject(Make(tier: 3, level: 20, seconds: 2), ExistingLadder()),
                Is.Not.Null);
        }

        [Test]
        public void ATierGatheringSlowerThanTheOneAbove_IsRejected()
        {
            Assert.That(MaterialValidation.Reject(Make(tier: 3, level: 20, seconds: 99), ExistingLadder()),
                Is.Not.Null);
        }

        [Test]
        public void ATierPayingLessXpPerHourThanTheOneBelow_IsRejected()
        {
            // XpPerHour_NeverFallsAsTiersRise — the rule §4.1a actually cares about. Tier 2
            // pays 12 XP over 5s (2.4/s); this pays 9 over 9s (1/s), so advancing would cut
            // the player's income.
            var worse = Make(tier: 3, level: 20, seconds: 9, xp: 9);

            var rejection = MaterialValidation.Reject(worse, ExistingLadder());

            Assert.That(rejection, Is.Not.Null);
            Assert.That(rejection, Does.Contain("per hour"));
        }

        [Test]
        public void ATierPayingMoreXpPerHour_IsAccepted()
        {
            // The prescribed ladder rises rather than staying flat, so this must pass.
            Assert.That(MaterialValidation.Reject(Make(tier: 3, level: 20, seconds: 9, xp: 25), ExistingLadder()),
                Is.Null);
        }

        // ── Nonsense values ─────────────────────────────────────────────

        [Test]
        public void FreeOrNegativeGathering_IsRejected()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MaterialValidation.Reject(Make(seconds: 0), ExistingLadder()), Is.Not.Null);
                Assert.That(MaterialValidation.Reject(Make(seconds: -5), ExistingLadder()), Is.Not.Null);
            });
        }

        [Test]
        public void NegativeXpOrLevel_IsRejected()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MaterialValidation.Reject(Make(xp: -1), ExistingLadder()), Is.Not.Null);
                Assert.That(MaterialValidation.Reject(Make(level: 0), ExistingLadder()), Is.Not.Null);
            });
        }

        // ── Drop tables ─────────────────────────────────────────────────

        [Test]
        public void AProducedMaterial_CannotBeADrop()
        {
            // ProductionMaterials_NeverAppearInDropTables — a cooked pie must not be found
            // lying in a field.
            var pie = Make("meat_pie", SkillType.Cooking, MaterialCategory.Cooked);

            Assert.That(
                MaterialValidation.RejectAsDrop(pie, SkillSeedData.ProductionSkills),
                Is.Not.Null);
        }

        [Test]
        public void AGatheredMaterial_CanBeADrop()
        {
            Assert.That(
                MaterialValidation.RejectAsDrop(Make(), SkillSeedData.ProductionSkills),
                Is.Null);
        }

        [Test]
        public void AUniversalMaterial_CanBeADrop()
        {
            // Dust is what an Open cell yields, so it must survive this check.
            var dust = Make("dust", skill: null, category: MaterialCategory.Dust);

            Assert.That(
                MaterialValidation.RejectAsDrop(dust, SkillSeedData.ProductionSkills),
                Is.Null);
        }
    }
}
