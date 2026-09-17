using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Admin;
using GeoSlayer.Domain.Services.Museum;

namespace GeoSlayer.Tests.Services.Admin
{
    /// <summary>
    /// Museum entry rules (Stage 18 task 8).
    ///
    /// <para>The Museum's premise is that it is optional. Nothing crashes if a wing bonus is
    /// doubled — the game simply becomes one where the Museum is compulsory, a design
    /// regression nobody would ever file a bug about. These rules keep that from happening
    /// from an admin screen.</para>
    /// </summary>
    [TestFixture]
    public class MuseumValidationTests
    {
        private static MuseumEntryDefinition Entry(
            string key = "new_entry",
            MuseumWing wing = MuseumWing.Naturalist) => new()
            {
                Key = key,
                Name = "A Find",
                Description = "",
                Wing = wing,
                Rarity = MuseumRarity.Common,
                UnlockCondition = "Find one.",
                SortOrder = 1,
            };

        // ── Entries ─────────────────────────────────────────────────────

        [Test]
        public void AWellFormedEntry_IsAccepted()
        {
            Assert.That(MuseumValidation.Reject(Entry(), []), Is.Null);
        }

        [Test]
        public void ADuplicateKey_IsRejected()
        {
            // RecordFind matches on the key — a collision credits a find to the wrong plinth.
            var existing = new List<MuseumEntryDefinition> { Entry("shared") };

            Assert.That(MuseumValidation.Reject(Entry("shared"), existing), Is.Not.Null);
        }

        [Test]
        public void AnEntryWithNoUnlockCondition_IsRejected()
        {
            // §5A.1 stakes the system on an empty plinth reading as a pull rather than as
            // noise, and the condition is what it says.
            var blank = Entry();
            blank.UnlockCondition = "  ";

            Assert.That(MuseumValidation.Reject(blank, []), Is.Not.Null);
        }

        [Test]
        public void AnEntryWithNoKeyOrName_IsRejected()
        {
            var noKey = Entry();
            noKey.Key = "";

            var noName = Entry();
            noName.Name = " ";

            Assert.Multiple(() =>
            {
                Assert.That(MuseumValidation.Reject(noKey, []), Is.Not.Null);
                Assert.That(MuseumValidation.Reject(noName, []), Is.Not.Null);
            });
        }

        // ── Set bonuses: the design regression ──────────────────────────

        [Test]
        public void ASmallBonus_IsAccepted()
        {
            Assert.That(MuseumValidation.RejectSetBonus(MuseumWing.Naturalist, 0.05), Is.Null);
        }

        [Test]
        public void ABonusAtTheCap_IsAccepted()
        {
            Assert.That(
                MuseumValidation.RejectSetBonus(MuseumWing.Naturalist, MuseumValidation.MaxSetBonus),
                Is.Null);
        }

        [Test]
        public void ALargeBonus_IsRejected()
        {
            // The whole point. A wing is dozens of finds; a large bonus makes filling it
            // compulsory rather than a choice.
            var rejection = MuseumValidation.RejectSetBonus(MuseumWing.Naturalist, 0.5);

            Assert.That(rejection, Is.Not.Null);
            Assert.That(rejection, Does.Contain("nudge"));
        }

        [Test]
        public void AZeroBonus_IsRejected()
        {
            // §4.3: a bonus that changes nothing is a bug, not restraint.
            Assert.That(MuseumValidation.RejectSetBonus(MuseumWing.Naturalist, 0), Is.Not.Null);
        }

        [Test]
        public void ABonusForCartography_IsRejected()
        {
            // Its entries are created on discovery, so the wing has no fixed size and can
            // never be completed. A bonus would be a promise that cannot be kept.
            var rejection = MuseumValidation.RejectSetBonus(MuseumWing.Cartography, 0.05);

            Assert.That(rejection, Is.Not.Null);
            Assert.That(rejection, Does.Contain("never be completed"));
        }

        [Test]
        public void TheShippedBonuses_AllPassTheirOwnValidator()
        {
            // If what shipped cannot pass, one of the two is wrong.
            foreach (var (wing, bonus) in MuseumSetBonus.Bonuses)
            {
                Assert.That(MuseumValidation.RejectSetBonus(wing, bonus.Value), Is.Null,
                    $"{wing}'s shipped bonus is invalid");
            }
        }

        [Test]
        public void NoShippedBonus_ExistsForAnUncompletableWing()
        {
            foreach (var wing in MuseumValidation.UncompletableWings)
            {
                Assert.That(MuseumSetBonus.Bonuses.ContainsKey(wing), Is.False,
                    $"{wing} cannot be completed but has a bonus");
            }
        }

        // ── Warnings ────────────────────────────────────────────────────

        [Test]
        public void AWingThatFillsByAccident_IsWarnedAbout()
        {
            var sparse = new List<MuseumEntryDefinition>
            {
                Entry("a", MuseumWing.Naturalist),
                Entry("b", MuseumWing.Naturalist),
            };

            var warnings = MuseumValidation.Warn(sparse);

            Assert.That(warnings.Any(w => w.Contains("freebie")), Is.True);
        }

        [Test]
        public void AnEmptyWing_IsWarnedAbout()
        {
            var warnings = MuseumValidation.Warn([Entry("a", MuseumWing.Naturalist)]);

            Assert.That(warnings.Any(w => w.Contains("empty headings")), Is.True);
        }

        [Test]
        public void CartographyIsNotWarnedAboutForBeingSmall()
        {
            // It has no fixed size by design, so "too few entries" is not a criticism of it.
            var entries = new List<MuseumEntryDefinition> { Entry("region", MuseumWing.Cartography) };

            var warnings = MuseumValidation.Warn(entries);

            Assert.That(warnings.Any(w => w.Contains("Cartography") && w.Contains("freebie")),
                Is.False);
        }
    }
}
