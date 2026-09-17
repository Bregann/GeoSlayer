using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api.Museum;
using GeoSlayer.Domain.Services.Museum;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;

namespace GeoSlayer.Tests.Services.Museum
{
    /// <summary>
    /// Stage 12 criteria 2–8, against a real database.
    ///
    /// <para>The region resolver is faked throughout — no test may depend on a third-party
    /// geocoder, for the same reason none may reach Overpass.</para>
    /// </summary>
    [TestFixture]
    public class MuseumIntegrationTests : DatabaseIntegrationTestBase
    {
        private MuseumService _sut = null!;
        private ProgressionService _progression = null!;
        private Player _player = null!;

        private static CancellationToken Ct => CancellationToken.None;

        protected override async Task CustomSetUp()
        {
            var (_, player) = await TestDatabaseSeedHelper.SeedMinimalData(DbContext);
            _player = player;

            await TestDatabaseSeedHelper.SeedProgressionDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedMaterialDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedSkillDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedCraftingDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedMuseumDefinitions(DbContext);

            _progression = TestDatabaseSeedHelper.CreateProgressionService(DbContext);
            await _progression.EnsureStartingUnlocks(_player.Id, Ct);

            _sut = TestDatabaseSeedHelper.CreateMuseumService(DbContext);
        }

        // ── Criterion 2: a POI type fills its Landmark plinth ───────────

        [Test]
        public async Task RecordingAFind_StoresWhereAndWhen()
        {
            // The diary, not a tally: "found at Durham Cathedral, 3 May" is the entry.
            var before = DateTime.UtcNow;

            var acquisition = await _sut.RecordFind(
                _player.Id, MuseumSeedData.LandmarkKey(SkillType.Prayer),
                1, 42, "Durham Cathedral", Ct);

            var entry = await DbContext.PlayerMuseumEntries
                .FirstAsync(e => e.PlayerId == _player.Id);

            Assert.Multiple(() =>
            {
                Assert.That(acquisition, Is.Not.Null);
                Assert.That(entry.AcquiredAtPoiId, Is.EqualTo(42));
                Assert.That(entry.AcquiredAtName, Is.EqualTo("Durham Cathedral"));
                Assert.That(entry.FirstAcquiredUtc, Is.GreaterThanOrEqualTo(before.AddSeconds(-5)));
            });
        }

        [Test]
        public async Task AFirstFind_ReturnsAnAcquisitionForTheCelebration()
        {
            var acquisition = await _sut.RecordFind(
                _player.Id, MuseumSeedData.TerrainKey(TerrainType.Woodland), 1, null, null, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(acquisition, Is.Not.Null);
                Assert.That(acquisition!.Wing, Is.EqualTo(MuseumWing.Naturalist));
                Assert.That(acquisition.Name, Is.Not.Empty);
            });
        }

        // ── Criterion 5: duplicates increment, never duplicate rows ─────

        [Test]
        public async Task ARepeatFind_IncrementsQuantityWithoutASecondRow()
        {
            var key = MuseumSeedData.TerrainKey(TerrainType.Water);

            await _sut.RecordFind(_player.Id, key, 1, null, null, Ct);
            await _sut.RecordFind(_player.Id, key, 3, null, null, Ct);

            var entries = await DbContext.PlayerMuseumEntries
                .Where(e => e.PlayerId == _player.Id && e.EntryKey == key)
                .ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(entries, Has.Count.EqualTo(1), "the unique index is the guarantee");
                Assert.That(entries[0].Quantity, Is.EqualTo(4));
            });
        }

        [Test]
        public async Task ARepeatFind_DoesNotOverwriteTheOriginalDate()
        {
            // First-find is what counts (§5A.3). A later find must not rewrite the diary.
            var key = MuseumSeedData.TerrainKey(TerrainType.Farmland);

            await _sut.RecordFind(_player.Id, key, 1, 7, "First Place", Ct);

            var original = await DbContext.PlayerMuseumEntries
                .Where(e => e.PlayerId == _player.Id && e.EntryKey == key)
                .Select(e => new { e.FirstAcquiredUtc, e.AcquiredAtName })
                .FirstAsync();

            await _sut.RecordFind(_player.Id, key, 1, 99, "Second Place", Ct);

            var after = await DbContext.PlayerMuseumEntries
                .FirstAsync(e => e.PlayerId == _player.Id && e.EntryKey == key);

            Assert.Multiple(() =>
            {
                // Within a tick: Postgres stores microseconds, so a value read back differs
                // from the in-memory one in its last digit. The point is that it was not
                // *rewritten*, not that it round-trips bit-identically.
                Assert.That(after.FirstAcquiredUtc, Is.EqualTo(original.FirstAcquiredUtc)
                    .Within(TimeSpan.FromMilliseconds(1)));
                Assert.That(after.AcquiredAtName, Is.EqualTo("First Place"));
            });
        }

        [Test]
        public async Task ARepeatFind_ReturnsNoAcquisition()
        {
            // Only a first-find is a moment worth celebrating.
            var key = MuseumSeedData.TerrainKey(TerrainType.Coastal);

            await _sut.RecordFind(_player.Id, key, 1, null, null, Ct);
            var second = await _sut.RecordFind(_player.Id, key, 1, null, null, Ct);

            Assert.That(second, Is.Null);
        }

        // ── Criterion 3: regions, and criterion 8: the cache ────────────

        [Test]
        public async Task EnteringARegion_AddsACartographyEntry()
        {
            var acquisition = await _sut.RecordRegion(_player.Id, 54.7761, -1.5733, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(acquisition, Is.Not.Null);
                Assert.That(acquisition!.Wing, Is.EqualTo(MuseumWing.Cartography));
                Assert.That(acquisition.Name, Is.EqualTo("Testshire"));
            });
        }

        [Test]
        public async Task ReEnteringARegion_AddsNothing()
        {
            await _sut.RecordRegion(_player.Id, 54.7761, -1.5733, Ct);

            // Same coarse cell, slightly different coordinates.
            var second = await _sut.RecordRegion(_player.Id, 54.7765, -1.5730, Ct);

            var count = await DbContext.PlayerMuseumEntries
                .CountAsync(e => e.PlayerId == _player.Id && e.EntryKey.StartsWith("region:"));

            Assert.Multiple(() =>
            {
                Assert.That(second, Is.Null, "a repeat visit is not a new plinth");
                Assert.That(count, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task ReverseGeocoding_IsCachedAcrossPlayers()
        {
            // Criterion 8. The cache is shared, so the second player to walk a cell triggers
            // no lookup — which is what makes the policy-restricted geocoder safe to use.
            var resolver = new Mock<IRegionResolver>();

            resolver
                .Setup(r => r.Resolve(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RegionResult("region:54.75:-1.60", "Testshire", "Locality"));

            var sut = new MuseumService(DbContext, resolver.Object);

            await sut.RecordRegion(_player.Id, 54.7761, -1.5733, Ct);

            var otherUser = await TestDatabaseSeedHelper.SeedTestUser(DbContext, "second_walker");
            var other = await TestDatabaseSeedHelper.SeedTestPlayer(DbContext, otherUser);

            await sut.RecordRegion(other.Id, 54.7761, -1.5733, Ct);
            await sut.RecordRegion(_player.Id, 54.7761, -1.5733, Ct);

            resolver.Verify(
                r => r.Resolve(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()),
                Times.Once,
                "a region must be resolved once, ever — not once per player");
        }

        [Test]
        public async Task AnUnresolvableRegion_AddsNothingRatherThanInventingAPlace()
        {
            var sut = TestDatabaseSeedHelper.CreateMuseumService(DbContext, regionName: null);

            var acquisition = await sut.RecordRegion(_player.Id, 10.0, 10.0, Ct);

            Assert.That(acquisition, Is.Null);
        }

        [Test]
        public async Task TwoPlayersInTheSameRegion_EachGetTheirOwnPlinth()
        {
            await _sut.RecordRegion(_player.Id, 54.7761, -1.5733, Ct);

            var otherUser = await TestDatabaseSeedHelper.SeedTestUser(DbContext, "third_walker");
            var other = await TestDatabaseSeedHelper.SeedTestPlayer(DbContext, otherUser);

            var theirs = await _sut.RecordRegion(other.Id, 54.7761, -1.5733, Ct);

            Assert.That(theirs, Is.Not.Null,
                "a shared cache must not mean a shared collection");
        }

        // ── Criterion 4: materials fill their skill-wing plinth ─────────

        [Test]
        public async Task GatheringAMaterial_AddsItsSkillWingEntry()
        {
            var acquisitions = await _sut.RecordCellFinds(
                _player.Id,
                [TerrainType.Woodland],
                ["wild_grass"],
                Ct);

            Assert.Multiple(() =>
            {
                Assert.That(acquisitions.Any(a => a.Wing == MuseumWing.Naturalist), Is.True);
                Assert.That(acquisitions.Any(a => a.Wing == MuseumWing.Skills), Is.True);
            });
        }

        [Test]
        public async Task OpenTerrain_HasNoPlinth()
        {
            // Open means "nothing identifiable" — it is not a terrain you can say you crossed.
            var acquisitions = await _sut.RecordCellFinds(_player.Id, [TerrainType.Open], [], Ct);

            Assert.That(acquisitions, Is.Empty);
        }

        // ── Criterion 6: donating keeps the entry ───────────────────────

        [Test]
        public async Task DonatingDuplicates_YieldsCurationAndKeepsTheEntry()
        {
            var key = MuseumSeedData.MaterialKey("wild_grass");

            await _sut.RecordFind(_player.Id, key, 5, null, null, Ct);

            var result = await _sut.DonateDuplicates(_player.Id, key, 2, Ct);

            var entry = await DbContext.PlayerMuseumEntries
                .FirstOrDefaultAsync(e => e.PlayerId == _player.Id && e.EntryKey == key);

            Assert.Multiple(() =>
            {
                Assert.That(result.CurationEarned, Is.GreaterThan(0));
                Assert.That(result.EntryRetained, Is.True);
                Assert.That(entry, Is.Not.Null, "donating must never remove the plinth");
                Assert.That(entry!.Quantity, Is.EqualTo(5), "the record of what was found stands");
            });
        }

        [Test]
        public async Task TheDisplayPiece_CannotBeDonated()
        {
            // One is always kept on the plinth. Donating the last would make the Museum a
            // shop, and §5A says entries are permanent.
            var key = MuseumSeedData.MaterialKey("scrap");

            await _sut.RecordFind(_player.Id, key, 1, null, null, Ct);

            await Assert.ThatAsync(
                () => _sut.DonateDuplicates(_player.Id, key, 1, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task TheSameSpare_CannotBeDonatedTwice()
        {
            var key = MuseumSeedData.MaterialKey("scrap");

            await _sut.RecordFind(_player.Id, key, 3, null, null, Ct);
            await _sut.DonateDuplicates(_player.Id, key, 2, Ct);

            await Assert.ThatAsync(
                () => _sut.DonateDuplicates(_player.Id, key, 1, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task DonatingWhatYouHaveNotFound_IsRejected()
        {
            await Assert.ThatAsync(
                () => _sut.DonateDuplicates(_player.Id, MuseumSeedData.MaterialKey("berries"), 1, Ct),
                Throws.TypeOf<NotFoundException>());
        }

        [Test]
        public async Task RarerEntries_PayMoreCuration()
        {
            var common = MuseumSeedData.MaterialKey("wild_grass");
            var legendary = MuseumSeedData.MaterialKey("blessed_water");

            await _sut.RecordFind(_player.Id, common, 3, null, null, Ct);
            await _sut.RecordFind(_player.Id, legendary, 3, null, null, Ct);

            var commonResult = await _sut.DonateDuplicates(_player.Id, common, 1, Ct);
            var legendaryResult = await _sut.DonateDuplicates(_player.Id, legendary, 1, Ct);

            Assert.That(legendaryResult.CurationEarned, Is.GreaterThan(commonResult.CurationEarned));
        }

        // ── Criterion 7: permanence ─────────────────────────────────────

        [Test]
        public void NoCodePath_DeletesAMuseumEntry()
        {
            // §5A frames permanence as the point: the Museum is the only system that never
            // decays, caps or resets. A removal call is therefore a design bug, not just a
            // risky one — so this greps for it rather than trusting review.
            var root = FindRepositoryRoot();
            var servicesDir = Path.Combine(root, "GeoSlayer.Domain", "Services");

            var offenders = new List<string>();

            foreach (var file in Directory.GetFiles(servicesDir, "*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);

                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];

                    if (line.Contains("PlayerMuseumEntries.Remove")
                        || line.Contains("PlayerMuseumEntries.RemoveRange")
                        || line.Contains("PlayerMuseumEntries.ExecuteDelete"))
                    {
                        offenders.Add($"{Path.GetFileName(file)}:{i + 1}  {line.Trim()}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty,
                "Museum entries are permanent:\n" + string.Join("\n", offenders));
        }

        [Test]
        public async Task TheMuseum_ShowsEmptyPlinthsNotJustFoundOnes()
        {
            // §5A.1: "empty plinths are a stronger pull than empty checkboxes". A wing that
            // only listed what you have would be a receipt.
            await _sut.RecordFind(_player.Id, MuseumSeedData.TerrainKey(TerrainType.Woodland), 1, null, null, Ct);

            var museum = await _sut.GetMuseum(_player.Id, Ct);
            var naturalist = museum.Wings.First(w => w.Wing == MuseumWing.Naturalist);

            Assert.Multiple(() =>
            {
                Assert.That(naturalist.Found, Is.EqualTo(1));
                Assert.That(naturalist.Total, Is.GreaterThan(1), "the unfound ones must still show");
                Assert.That(naturalist.Entries.Any(e => !e.IsFound), Is.True);
                Assert.That(naturalist.Entries.First(e => !e.IsFound).UnlockCondition, Is.Not.Empty,
                    "an empty plinth should say how to fill it");
            });
        }

        [Test]
        public async Task EveryWingAppears_EvenTheEmptyOnes()
        {
            // Relics and Expeditions have no entries until Stages 13-14. An absent wing reads
            // as "not in this game"; an empty one reads as "not yet".
            var museum = await _sut.GetMuseum(_player.Id, Ct);

            Assert.That(museum.Wings.Select(w => w.Wing),
                Is.EquivalentTo(Enum.GetValues<MuseumWing>()));
        }

        [Test]
        public async Task AnIncompleteWing_IsNotMarkedComplete()
        {
            var museum = await _sut.GetMuseum(_player.Id, Ct);

            foreach (var wing in museum.Wings.Where(w => w.Total > 0 && w.Found < w.Total))
            {
                Assert.That(wing.IsComplete, Is.False, $"{wing.Name} is not complete");
            }
        }

        [Test]
        public async Task AnEmptyWing_IsNotMarkedComplete()
        {
            // Zero of zero is not an achievement.
            var museum = await _sut.GetMuseum(_player.Id, Ct);

            foreach (var wing in museum.Wings.Where(w => w.Total == 0))
            {
                Assert.That(wing.IsComplete, Is.False, $"{wing.Name} has no entries yet");
            }
        }

        // ── Feats, derived from existing counters ───────────────────────

        [Test]
        public async Task AFeat_IsAwardedOnceItsThresholdIsMet()
        {
            var now = DateTime.UtcNow;

            for (var i = 0; i < 100; i++)
            {
                DbContext.RevealedCells.Add(new RevealedCell
                {
                    PlayerId = _player.Id,
                    GridLat = i,
                    GridLng = 0,
                    RevealedAtUtc = now,
                });
            }

            await DbContext.SaveChangesAsync();

            var acquisitions = await _sut.CheckFeats(_player.Id, Ct);

            Assert.That(acquisitions.Any(a => a.Key == MuseumSeedData.FeatKey("cells_100")), Is.True);
        }

        [Test]
        public async Task AFeat_IsNotAwardedTwice()
        {
            var now = DateTime.UtcNow;

            for (var i = 0; i < 100; i++)
            {
                DbContext.RevealedCells.Add(new RevealedCell
                {
                    PlayerId = _player.Id,
                    GridLat = i,
                    GridLng = 0,
                    RevealedAtUtc = now,
                });
            }

            await DbContext.SaveChangesAsync();

            await _sut.CheckFeats(_player.Id, Ct);
            var second = await _sut.CheckFeats(_player.Id, Ct);

            Assert.That(second, Is.Empty);
        }

        [Test]
        public async Task AFeatBelowItsThreshold_IsNotAwarded()
        {
            var acquisitions = await _sut.CheckFeats(_player.Id, Ct);

            Assert.That(acquisitions.Any(a => a.Key == MuseumSeedData.FeatKey("cells_1000")), Is.False);
        }

        // ── Wings are derived, not authored ─────────────────────────────

        [Test]
        public void EveryPoiSkill_HasALandmarkPlinth()
        {
            // §5A.2's claim is that the ~80-entry tag table becomes content for free. This
            // holds it to that: a skill added to the enum gets a plinth without anyone
            // remembering to add one.
            var keys = MuseumSeedData.Definitions()
                .Where(d => d.Wing == MuseumWing.Landmarks)
                .Select(d => d.Key)
                .ToHashSet();

            foreach (var skill in Enum.GetValues<SkillType>())
            {
                Assert.That(keys, Contains.Item(MuseumSeedData.LandmarkKey(skill)));
            }
        }

        [Test]
        public void EverySeededMaterial_HasAPlinth()
        {
            var keys = MuseumSeedData.Definitions().Select(d => d.Key).ToHashSet();

            var materials = Domain.Services.Materials.MaterialSeedData.Materials
                .Concat(Domain.Services.Skills.SkillSeedData.AllSkillMaterials)
                .Where(m => m.Category != MaterialCategory.Dust);

            foreach (var material in materials)
            {
                Assert.That(keys, Contains.Item(MuseumSeedData.MaterialKey(material.Key)),
                    $"{material.Key} has no Museum plinth");
            }
        }

        [Test]
        public void PlinthKeysAreUnique()
        {
            var keys = MuseumSeedData.Definitions().Select(d => d.Key).ToList();

            Assert.That(keys, Is.Unique);
        }

        // ── Set bonuses (Stage 12 task 3, delivered in Stage 14) ───────

        [Test]
        public async Task AnIncompleteWing_GrantsNoSetBonus()
        {
            var total = await _sut.GetSetBonusTotal(_player.Id, ItemModifier.StackCapPercent, Ct);

            Assert.That(total, Is.Zero);
        }

        [Test]
        public async Task ACompletedWing_GrantsItsSetBonus()
        {
            // Fill the Naturalist wing entirely.
            var keys = await DbContext.MuseumEntryDefinitions
                .Where(d => d.Wing == MuseumWing.Naturalist)
                .Select(d => d.Key)
                .ToListAsync();

            foreach (var key in keys)
            {
                await _sut.RecordFind(_player.Id, key, 1, null, null, Ct);
            }

            var completed = await _sut.GetCompletedWings(_player.Id, Ct);

            var bonus = await _sut.GetSetBonusTotal(_player.Id, ItemModifier.SkillXpPercent, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(completed, Contains.Item(MuseumWing.Naturalist));
                Assert.That(bonus, Is.GreaterThan(0), "a filled wing should actually grant something");
            });
        }

        [Test]
        public async Task SetBonusesAreSmall()
        {
            // §5A / Stage 12 task 3: "keep small — the Museum should be pursued for its own
            // sake, not because it is mandatory". A wing is dozens of finds; a large bonus
            // would make filling it obligatory rather than a choice.
            foreach (var (_, bonus) in MuseumSetBonus.Bonuses)
            {
                if (bonus.Modifier is ItemModifier.SkillXpPercent
                    or ItemModifier.StackCapPercent
                    or ItemModifier.WorkerRatePercent)
                {
                    Assert.That(bonus.Value, Is.LessThanOrEqualTo(0.10),
                        $"{bonus.Description} is more than a nudge");
                }
            }

            await Task.CompletedTask;
        }

        [Test]
        public void CartographyHasNoSetBonus()
        {
            // That wing has no fixed size — regions are created on discovery — so it can
            // never be "complete", and promising a bonus for finishing it would be a lie.
            Assert.That(MuseumSetBonus.Bonuses.ContainsKey(MuseumWing.Cartography), Is.False);
        }

        [Test]
        public async Task ACompletedWing_IsReportedOnTheMuseumDto()
        {
            var keys = await DbContext.MuseumEntryDefinitions
                .Where(d => d.Wing == MuseumWing.Naturalist)
                .Select(d => d.Key)
                .ToListAsync();

            foreach (var key in keys)
            {
                await _sut.RecordFind(_player.Id, key, 1, null, null, Ct);
            }

            var museum = await _sut.GetMuseum(_player.Id, Ct);
            var naturalist = museum.Wings.First(w => w.Wing == MuseumWing.Naturalist);

            Assert.Multiple(() =>
            {
                Assert.That(naturalist.IsComplete, Is.True);
                Assert.That(naturalist.SetBonusActive, Is.True);
                Assert.That(naturalist.SetBonusDescription, Is.Not.Null);
            });
        }

        [Test]
        public async Task AnIncompleteWing_StillShowsWhatItsBonusWouldBe()
        {
            // The reward should be visible as a reason to fill the wing, not a surprise at
            // the end.
            var museum = await _sut.GetMuseum(_player.Id, Ct);
            var naturalist = museum.Wings.First(w => w.Wing == MuseumWing.Naturalist);

            Assert.Multiple(() =>
            {
                Assert.That(naturalist.SetBonusDescription, Is.Not.Null);
                Assert.That(naturalist.SetBonusActive, Is.False);
            });
        }

        private static string FindRepositoryRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GeoSlayer.sln")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
        }
    }
}
