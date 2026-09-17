using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Services.Admin;
using GeoSlayer.Domain.Services.Combat;

namespace GeoSlayer.Tests.Services.Admin
{
    /// <summary>
    /// Encounter rules (Stage 18 task 8).
    ///
    /// <para>The important one is §5C.2's geography rule: a player with no castle must be
    /// able to train Combat to the top. Stage 16 proved it over seed data; these tests make
    /// it survive runtime editing, where the symptom of breaking it would be a player quietly
    /// unable to progress — close to undiagnosable from a bug report.</para>
    /// </summary>
    [TestFixture]
    public class EncounterValidationTests
    {
        private static EncounterDefinition Make(
            string key = "new_encounter",
            int tier = 3,
            int minLevel = 20,
            bool trainingGround = false) => new()
            {
                Key = key,
                Name = "A Fight",
                Description = "",
                Tier = tier,
                MinCombatLevel = minLevel,
                IsTrainingGround = trainingGround,
            };

        /// <summary>A roaming ladder covering all seven tiers — the valid baseline.</summary>
        private static List<EncounterDefinition> FullRoamingLadder() =>
        [
            Make("roam_1", 1, 1),
            Make("roam_2", 2, 10),
            Make("roam_3", 3, 20),
            Make("roam_4", 4, 35),
            Make("roam_5", 5, 50),
            Make("roam_6", 6, 70),
            Make("roam_7", 7, 90),
        ];

        // ── The individual row ──────────────────────────────────────────

        [Test]
        public void AWellFormedEncounter_IsAccepted()
        {
            Assert.That(EncounterValidation.Reject(Make("fresh"), FullRoamingLadder()), Is.Null);
        }

        [Test]
        public void ADuplicateKey_IsRejected()
        {
            Assert.That(
                EncounterValidation.Reject(Make("roam_3"), FullRoamingLadder()),
                Is.Not.Null);
        }

        [Test]
        public void AMissingKeyOrName_IsRejected()
        {
            var noKey = Make();
            noKey.Key = " ";

            var noName = Make();
            noName.Name = "";

            Assert.Multiple(() =>
            {
                Assert.That(EncounterValidation.Reject(noKey, FullRoamingLadder()), Is.Not.Null);
                Assert.That(EncounterValidation.Reject(noName, FullRoamingLadder()), Is.Not.Null);
            });
        }

        [Test]
        public void ATierOffTheLadder_IsRejected()
        {
            Assert.Multiple(() =>
            {
                Assert.That(EncounterValidation.Reject(Make("a", tier: 0), FullRoamingLadder()), Is.Not.Null);
                Assert.That(EncounterValidation.Reject(Make("b", tier: 8), FullRoamingLadder()), Is.Not.Null);
            });
        }

        [Test]
        public void GatingTheBottomRung_IsRejected()
        {
            // §5C.1: tier 1 sits at level 1 so a new player always has something to find.
            // Gating it means a level-1 player meets nothing at all.
            Assert.That(
                EncounterValidation.Reject(Make("gated", tier: 1, minLevel: 15), FullRoamingLadder()),
                Is.Not.Null);
        }

        [Test]
        public void TierOneAtLevelOne_IsFine()
        {
            Assert.That(
                EncounterValidation.Reject(Make("starter", tier: 1, minLevel: 1), []),
                Is.Null);
        }

        // ── §5C.2: the geography rule ───────────────────────────────────

        [Test]
        public void AFullRoamingLadder_IsAccepted()
        {
            Assert.That(EncounterValidation.RejectSet(FullRoamingLadder()), Is.Null);
        }

        [Test]
        public void AGapInTheRoamingLadder_IsRejected()
        {
            // The whole point. Remove tier 5 from roaming and a player with no historic
            // ground cannot pass it.
            var gapped = FullRoamingLadder().Where(e => e.Tier != 5).ToList();

            var rejection = EncounterValidation.RejectSet(gapped);

            Assert.That(rejection, Is.Not.Null);
            Assert.That(rejection, Does.Contain("5"), "the message should name the missing tier");
        }

        [Test]
        public void ATierCoveredOnlyByATrainingGround_IsRejected()
        {
            // The subtler failure: the tier exists, but only on historic ground. That makes
            // a castle a requirement rather than a boost, which §5C.2 forbids.
            var ladder = FullRoamingLadder();
            ladder.RemoveAll(e => e.Tier == 6);
            ladder.Add(Make("castle_6", tier: 6, minLevel: 70, trainingGround: true));

            Assert.That(EncounterValidation.RejectSet(ladder), Is.Not.Null);
        }

        [Test]
        public void EveryMissingTier_IsNamed()
        {
            // An admin fixing this needs to know the whole gap, not the first one.
            var sparse = FullRoamingLadder().Where(e => e.Tier is 1 or 2).ToList();

            var rejection = EncounterValidation.RejectSet(sparse);

            Assert.That(rejection, Is.Not.Null);

            foreach (var missing in new[] { "3", "4", "5", "6", "7" })
            {
                Assert.That(rejection, Does.Contain(missing));
            }
        }

        [Test]
        public void AnEmptySet_IsRejected()
        {
            // Deleting the last encounter would leave Combat untrainable entirely.
            Assert.That(EncounterValidation.RejectSet([]), Is.Not.Null);
        }

        [Test]
        public void TrainingGroundsAlongsideAFullLadder_AreFine()
        {
            var ladder = FullRoamingLadder();
            ladder.Add(Make("castle_3", tier: 3, minLevel: 20, trainingGround: true));

            Assert.That(EncounterValidation.RejectSet(ladder), Is.Null);
        }

        // ── Warnings ────────────────────────────────────────────────────

        [Test]
        public void NoTrainingGroundsAtAll_IsWarnedAboutButAllowed()
        {
            // Nothing breaks — roaming covers every tier — but historic ground would stop
            // meaning anything, which is a design regression rather than a bug.
            var warnings = EncounterValidation.Warn(FullRoamingLadder());

            Assert.That(warnings.Any(w => w.Contains("No training grounds")), Is.True);
            Assert.That(EncounterValidation.RejectSet(FullRoamingLadder()), Is.Null);
        }

        [Test]
        public void ATrainingGroundOutpayingRoaming_IsWarnedAbout()
        {
            // §5C.1's compensating advantage is availability, not reward. A castle paying a
            // higher tier at the same level makes it a requirement.
            var ladder = FullRoamingLadder();
            ladder.Add(Make("rich_castle", tier: 7, minLevel: 20, trainingGround: true));

            var warnings = EncounterValidation.Warn(ladder);

            Assert.That(warnings.Any(w => w.Contains("rich_castle")), Is.True);
        }

        [Test]
        public void ATrainingGroundMatchingRoaming_IsNotWarnedAbout()
        {
            // Identical pay at the same tier is exactly what Stage 16 shipped.
            var ladder = FullRoamingLadder();
            ladder.Add(Make("fair_castle", tier: 3, minLevel: 20, trainingGround: true));

            var warnings = EncounterValidation.Warn(ladder);

            Assert.That(warnings.Any(w => w.Contains("fair_castle")), Is.False);
        }

        // ── The seeded data still passes its own rules ──────────────────

        [Test]
        public void TheSeededEncounters_SatisfyTheGeographyRule()
        {
            // If the shipped seed data cannot pass this validator, one of the two is wrong.
            var seeded = EncounterSeedData.Encounters
                .Select(e => new EncounterDefinition
                {
                    Key = e.Key,
                    Name = e.Name,
                    Description = e.Description,
                    Tier = e.Tier,
                    MinCombatLevel = e.MinCombatLevel,
                    IsTrainingGround = e.IsTrainingGround,
                })
                .ToList();

            Assert.That(EncounterValidation.RejectSet(seeded), Is.Null);
        }
    }
}
