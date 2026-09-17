using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Admin;
using GeoSlayer.Domain.Services.Progression;

namespace GeoSlayer.Tests.Services.Admin
{
    /// <summary>
    /// Progression definition rules (Stage 18 task 8).
    ///
    /// <para>These definitions are read on hot paths by every account, so a bad save is not
    /// a local mistake. The cost curve is the sharpest case: <c>UpgradeDefinition.Costs</c>
    /// parses with <c>int.Parse</c>, which throws, and it is read every time the upgrades
    /// screen loads.</para>
    /// </summary>
    [TestFixture]
    public class ProgressionValidationTests
    {
        private static UpgradeDefinition Upgrade(
            string key = "new_upgrade",
            int maxRank = 5,
            string curve = "1,2,4,7,11",
            int minLevel = 1) => new()
            {
                Key = key,
                Name = "An Upgrade",
                Category = "Exploration",
                Description = "",
                MaxRank = maxRank,
                CostCurve = curve,
                EffectPerRank = 1,
                MinAdventurerLevel = minLevel,
            };

        private static UnlockDefinition Unlock(
            int level = 12,
            string payload = "Mining",
            string display = "Mining unlocked") => new()
            {
                AdventurerLevel = level,
                UnlockType = UnlockType.Skill,
                Payload = payload,
                DisplayName = display,
            };

        // ── Cost curves: the crash case ─────────────────────────────────

        [Test]
        public void AWellFormedCurve_IsAccepted()
        {
            Assert.That(ProgressionValidation.RejectCostCurve("1,2,4,7,11", 5), Is.Null);
        }

        [Test]
        public void ANonNumericCurve_IsRejected()
        {
            // The whole reason this validator exists: int.Parse throws, and Costs is read on
            // every upgrades-screen load. A typo here takes the screen down for everyone.
            var rejection = ProgressionValidation.RejectCostCurve("1,2,four,7", 4);

            Assert.That(rejection, Is.Not.Null);
            Assert.That(rejection, Does.Contain("four"), "the message should name the bad value");
        }

        [Test]
        public void AnEmptyCurve_IsRejected()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ProgressionValidation.RejectCostCurve(null, 1), Is.Not.Null);
                Assert.That(ProgressionValidation.RejectCostCurve("", 1), Is.Not.Null);
                Assert.That(ProgressionValidation.RejectCostCurve("   ", 1), Is.Not.Null);
                Assert.That(ProgressionValidation.RejectCostCurve(",,,", 1), Is.Not.Null);
            });
        }

        [Test]
        public void ACurveWithAFreeRank_IsRejected()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ProgressionValidation.RejectCostCurve("1,0,4", 3), Is.Not.Null);
                Assert.That(ProgressionValidation.RejectCostCurve("1,-2,4", 3), Is.Not.Null);
            });
        }

        [Test]
        public void ACurveThatDoesNotMatchMaxRank_IsRejected()
        {
            // A rank with no price cannot be bought; a price with no rank is never charged.
            Assert.Multiple(() =>
            {
                Assert.That(ProgressionValidation.RejectCostCurve("1,2,4", 5), Is.Not.Null);
                Assert.That(ProgressionValidation.RejectCostCurve("1,2,4,7,11", 3), Is.Not.Null);
            });
        }

        [Test]
        public void ACurveThatDips_IsRejected()
        {
            // §3.0a makes costs escalate. A dip means the cheapest route to a high rank is
            // to buy the expensive ones first — a puzzle rather than a choice.
            var rejection = ProgressionValidation.RejectCostCurve("1,5,3,9", 4);

            Assert.That(rejection, Is.Not.Null);
            Assert.That(rejection, Does.Contain("escalate"));
        }

        [Test]
        public void AFlatCurve_IsAccepted()
        {
            // Not escalating, but not dipping either — a flat price per rank is a legitimate
            // choice, so only a decrease is refused.
            Assert.That(ProgressionValidation.RejectCostCurve("3,3,3", 3), Is.Null);
        }

        [Test]
        public void WhitespaceInACurve_IsTolerated()
        {
            // The parser trims, so the validator must agree with it or a saveable curve
            // would be refused.
            Assert.That(ProgressionValidation.RejectCostCurve(" 1 , 2 , 4 ", 3), Is.Null);
        }

        [Test]
        public void EverySeededCurve_PassesItsOwnValidator()
        {
            // If the shipped curves cannot pass, one of the two is wrong.
            foreach (var upgrade in ProgressionSeedData.Upgrades)
            {
                Assert.That(
                    ProgressionValidation.RejectCostCurve(upgrade.CostCurve, upgrade.MaxRank),
                    Is.Null,
                    $"{upgrade.Key} has an invalid curve");
            }
        }

        // ── Upgrades ────────────────────────────────────────────────────

        [Test]
        public void AWellFormedUpgrade_IsAccepted()
        {
            Assert.That(ProgressionValidation.RejectUpgrade(Upgrade(), []), Is.Null);
        }

        [Test]
        public void ADuplicateUpgradeKey_IsRejected()
        {
            // The key is what PlayerUpgrade rows reference — a collision silently repoints
            // everyone's purchased ranks.
            var existing = new List<UpgradeDefinition> { Upgrade("reveal_radius") };

            Assert.That(
                ProgressionValidation.RejectUpgrade(Upgrade("reveal_radius"), existing),
                Is.Not.Null);
        }

        [Test]
        public void AnUpgradeWithNoRanks_IsRejected()
        {
            Assert.That(
                ProgressionValidation.RejectUpgrade(Upgrade(maxRank: 0, curve: "1"), []),
                Is.Not.Null);
        }

        [Test]
        public void AnUpgradeWithABadCurve_IsRejected()
        {
            Assert.That(
                ProgressionValidation.RejectUpgrade(Upgrade(curve: "1,oops,4,7,11"), []),
                Is.Not.Null);
        }

        [Test]
        public void AnUpgradeWithNoKeyOrName_IsRejected()
        {
            var noKey = Upgrade();
            noKey.Key = " ";

            var noName = Upgrade();
            noName.Name = "";

            Assert.Multiple(() =>
            {
                Assert.That(ProgressionValidation.RejectUpgrade(noKey, []), Is.Not.Null);
                Assert.That(ProgressionValidation.RejectUpgrade(noName, []), Is.Not.Null);
            });
        }

        // ── Unlocks ─────────────────────────────────────────────────────

        [Test]
        public void AWellFormedUnlock_IsAccepted()
        {
            Assert.That(ProgressionValidation.RejectUnlock(Unlock(), []), Is.Null);
        }

        [Test]
        public void TwoUnlocksOfTheSamePayloadAtOneLevel_AreRejected()
        {
            // Matches the unique index — caught here so the message is readable rather than
            // a constraint violation.
            var existing = new List<UnlockDefinition> { Unlock(12, "Mining") };

            Assert.That(
                ProgressionValidation.RejectUnlock(Unlock(12, "Mining"), existing),
                Is.Not.Null);
        }

        [Test]
        public void TheSamePayloadAtTwoLevels_IsRejected()
        {
            // The subtler one: ApplyUnlocks skips what is already owned, so the higher rung
            // silently never fires.
            var existing = new List<UnlockDefinition> { Unlock(12, "Mining") };

            var rejection = ProgressionValidation.RejectUnlock(Unlock(30, "Mining"), existing);

            Assert.That(rejection, Is.Not.Null);
            Assert.That(rejection, Does.Contain("12"), "the message should name the existing level");
        }

        [Test]
        public void AnUnlockWithNoPayloadOrName_IsRejected()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ProgressionValidation.RejectUnlock(Unlock(payload: " "), []), Is.Not.Null);
                Assert.That(ProgressionValidation.RejectUnlock(Unlock(display: ""), []), Is.Not.Null);
            });
        }

        [Test]
        public void AnUnlockBelowLevelOne_IsRejected()
        {
            Assert.That(ProgressionValidation.RejectUnlock(Unlock(level: 0), []), Is.Not.Null);
        }

        // ── The cold-start warning ──────────────────────────────────────

        [Test]
        public void ALadderWithNothingAtLevelOne_IsWarnedAbout()
        {
            // §3.1: a first walk that paints cells but drops nothing is a map-painting
            // utility, not an RPG.
            var warnings = ProgressionValidation.WarnUnlocks([Unlock(12, "Mining")]);

            Assert.That(warnings.Any(w => w.Contains("cold-start")), Is.True);
        }

        [Test]
        public void ALadderWithBothStartingSkills_IsNotWarnedAbout()
        {
            var ladder = new List<UnlockDefinition>
            {
                Unlock(1, "Exploration"),
                Unlock(1, "Foraging"),
            };

            Assert.That(ProgressionValidation.WarnUnlocks(ladder), Is.Empty);
        }

        [Test]
        public void TheSeededLadder_HasNoColdStartWarning()
        {
            Assert.That(ProgressionValidation.WarnUnlocks(ProgressionSeedData.Ladder), Is.Empty);
        }
    }
}
