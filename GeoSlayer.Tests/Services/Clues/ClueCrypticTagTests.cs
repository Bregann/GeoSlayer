using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Clues;

namespace GeoSlayer.Tests.Services.Clues
{
    /// <summary>
    /// Stage 13 criterion 5 — Cryptic clue generation.
    ///
    /// <para>The criterion's hard requirement is that a generated riddle resolve to
    /// <b>exactly one</b> POI in the search radius. An ambiguous riddle is unsolvable, which
    /// is worse than no Cryptic step at all — so most of these tests are about the
    /// generator declining to produce one.</para>
    /// </summary>
    [TestFixture]
    public class ClueCrypticTagTests
    {
        private static IReadOnlyDictionary<string, string> Tags(params (string Key, string Value)[] pairs) =>
            pairs.ToDictionary(p => p.Key, p => p.Value);

        // ── The uniqueness rule (criterion 5) ───────────────────────────

        [Test]
        public void ATagNoOtherPoiShares_BecomesADetail()
        {
            var candidate = Tags(("building:levels", "3"));
            var others = new[] { Tags(("building:levels", "1")) };

            var detail = ClueCrypticTags.DistinguishingDetail(candidate, others, seed: 1);

            Assert.That(detail, Is.Not.Null);
            Assert.That(detail, Does.Contain("three storeys"));
        }

        [Test]
        public void ATagAnotherPoiShares_IsRejected()
        {
            // Two churches of three storeys each. "Beneath three storeys" is a coin flip,
            // so it must not be generated at all.
            var candidate = Tags(("building:levels", "3"));
            var others = new[] { Tags(("building:levels", "3")) };

            Assert.That(ClueCrypticTags.DistinguishingDetail(candidate, others, seed: 1), Is.Null);
        }

        [Test]
        public void SharedTagIsRejected_RegardlessOfCase()
        {
            // OSM values are user-entered; "Brick" and "brick" are the same building.
            var candidate = Tags(("building:material", "Brick"));
            var others = new[] { Tags(("building:material", "brick")) };

            Assert.That(ClueCrypticTags.DistinguishingDetail(candidate, others, seed: 1), Is.Null);
        }

        [Test]
        public void OnlyTheSharedTagIsRejected_NotTheWholePoi()
        {
            // Shares its material, but nothing else has a tower. The riddle should use the
            // tower rather than give up.
            var candidate = Tags(("building:material", "brick"), ("tower:type", "bell_tower"));
            var others = new[] { Tags(("building:material", "brick")) };

            var detail = ClueCrypticTags.DistinguishingDetail(candidate, others, seed: 1);

            Assert.That(detail, Is.Not.Null);
            Assert.That(detail, Does.Contain("bell tower"));
            Assert.That(detail, Does.Not.Contain("brick"));
        }

        [Test]
        public void NoTags_YieldsNoDetail()
        {
            // Every POI imported before the Tags column existed looks like this. It must
            // degrade to a Category step, not throw and not invent a detail.
            Assert.That(
                ClueCrypticTags.DistinguishingDetail(Tags(), [Tags(("building:levels", "2"))], seed: 1),
                Is.Null);
        }

        [Test]
        public void NoRivals_StillNeedsAUsableTag()
        {
            // Alone in the radius, but carrying nothing that can be phrased.
            Assert.That(
                ClueCrypticTags.DistinguishingDetail(Tags(("source", "survey")), [], seed: 1),
                Is.Null);
        }

        // ── Phrasing: a riddle has to read like one ─────────────────────

        [Test]
        public void AMultiValuedTag_IsRejectedRatherThanReadAsProse()
        {
            // "built of brick;stone" reads as a database row, which is worse than no step.
            Assert.That(
                ClueCrypticTags.DistinguishingDetail(Tags(("building:material", "brick;stone")), [], seed: 1),
                Is.Null);
        }

        [Test]
        public void AnUnparseableStoreyCount_IsRejected()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ClueCrypticTags.DistinguishingDetail(Tags(("building:levels", "many")), [], 1), Is.Null);
                Assert.That(ClueCrypticTags.DistinguishingDetail(Tags(("building:levels", "0")), [], 1), Is.Null);
                Assert.That(ClueCrypticTags.DistinguishingDetail(Tags(("building:levels", "-2")), [], 1), Is.Null);
            });
        }

        [Test]
        public void AYearIsExtracted_FromOsmDateFormats()
        {
            var detail = ClueCrypticTags.DistinguishingDetail(Tags(("start_date", "1874-06-01")), [], seed: 1);

            Assert.That(detail, Is.EqualTo("that has stood since 1874"));
        }

        [Test]
        public void AnImplausibleYear_IsRejected()
        {
            Assert.That(
                ClueCrypticTags.DistinguishingDetail(Tags(("start_date", "0042")), [], seed: 1),
                Is.Null);
        }

        [Test]
        public void UnderscoresBecomeWords()
        {
            // OSM writes "bell_tower"; a riddle must not.
            var detail = ClueCrypticTags.DistinguishingDetail(Tags(("tower:type", "bell_tower")), [], seed: 1);

            Assert.That(detail, Does.Contain("bell tower"));
            Assert.That(detail, Does.Not.Contain("_"));
        }

        // ── Determinism: a carried scroll must not reword itself ────────

        [Test]
        public void TheSameSeed_GivesTheSameDetail()
        {
            var candidate = Tags(("building:levels", "4"), ("tower:type", "spire"), ("start_date", "1901"));

            var first = ClueCrypticTags.DistinguishingDetail(candidate, [], seed: 7);
            var second = ClueCrypticTags.DistinguishingDetail(candidate, [], seed: 7);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void ANegativeSeed_DoesNotThrow()
        {
            // HashCode.Combine routinely returns negatives, and ClueService seeds from it.
            Assert.That(
                () => ClueCrypticTags.DistinguishingDetail(Tags(("tower:type", "spire")), [], int.MinValue),
                Throws.Nothing);
        }

        // ── The riddle never names the place (§5B.2) ────────────────────

        [Test]
        public void TheRiddleNamesTheDetail_NeverThePoi()
        {
            var detail = ClueCrypticTags.DistinguishingDetail(Tags(("tower:type", "spire")), [], seed: 3);
            var riddle = ClueRiddleText.Cryptic(SkillType.Prayer, detail, seed: 3);

            Assert.Multiple(() =>
            {
                Assert.That(riddle, Does.Contain("spire"));

                // The category phrase, not the building's name — which the generator is
                // never even given.
                Assert.That(riddle, Does.StartWith("Find the place"));
            });
        }
    }
}
