using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Services.Clues;
using GeoSlayer.Domain.Services;
using GeoSlayer.Domain.Services.Fog;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace GeoSlayer.Tests.Services.Clues
{
    /// <summary>
    /// Stage 13 criteria 2, 3, 4, 6, 7, 8 and 9, against a real database.
    /// </summary>
    [TestFixture]
    public class ClueIntegrationTests : DatabaseIntegrationTestBase
    {
        private const double OriginLat = 51.5074;
        private const double OriginLng = -0.1278;

        private ClueService _sut = null!;
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
            await TestDatabaseSeedHelper.SeedMuseumDefinitions(DbContext);

            _progression = TestDatabaseSeedHelper.CreateProgressionService(DbContext);
            await _progression.EnsureStartingUnlocks(_player.Id, Ct);

            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext);
            var museum = TestDatabaseSeedHelper.CreateMuseumService(DbContext);

            _sut = new ClueService(DbContext, materials, museum);
        }

        /// <summary>Reveal a patch of ground, so generation has territory to anchor on.</summary>
        private async Task RevealTerritory(int count = 20)
        {
            var now = DateTime.UtcNow;
            var baseLat = FogService.ToGrid(OriginLat);
            var baseLng = FogService.ToGrid(OriginLng);

            var existing = (await DbContext.RevealedCells
                    .Where(r => r.PlayerId == _player.Id)
                    .Select(r => new { r.GridLat, r.GridLng })
                    .ToListAsync())
                .Select(r => (r.GridLat, r.GridLng))
                .ToHashSet();

            for (var i = 0; i < count; i++)
            {
                var cell = (baseLat + i, baseLng);
                if (!existing.Add(cell)) continue;

                DbContext.RevealedCells.Add(new RevealedCell
                {
                    PlayerId = _player.Id,
                    GridLat = cell.Item1,
                    GridLng = cell.Item2,
                    RevealedAtUtc = now,
                });
            }

            await DbContext.SaveChangesAsync();
        }

        /// <summary>Seed POIs near the revealed ground, as a real import would.</summary>
        private async Task SeedNearbyPois(int count = 10)
        {
            for (var i = 0; i < count; i++)
            {
                DbContext.PointsOfInterest.Add(new PointOfInterest
                {
                    OsmId = Random.Shared.NextInt64(1, long.MaxValue),
                    OsmType = "node",
                    Name = $"Test Place {i}",
                    Skill = i % 2 == 0 ? SkillType.Prayer : SkillType.Knowledge,
                    Location = new Point(OriginLng + i * 0.0005, OriginLat + i * 0.0005) { SRID = 4326 },
                    XpReward = 10 + i,
                });
            }

            await DbContext.SaveChangesAsync();
        }

        private async Task SetPosition(double lat, double lng)
        {
            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);
            player.LastLatitude = lat;
            player.LastLongitude = lng;
            player.LastSyncAtUtc = DateTime.UtcNow;
            await DbContext.SaveChangesAsync();
        }

        private async Task GiveCuration(int amount)
        {
            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);
            player.Curation += amount;
            await DbContext.SaveChangesAsync();
        }

        /// <summary>Stand exactly where the current step wants the player to be.</summary>
        private async Task StandAtCurrentStep(int scrollId)
        {
            var scroll = await DbContext.PlayerClueScrolls
                .Include(s => s.Steps)
                .FirstAsync(s => s.Id == scrollId);

            var step = scroll.Steps.First(s => s.StepIndex == scroll.CurrentStep);

            await SetPosition(step.TargetLat!.Value, step.TargetLng!.Value);
        }

        // ── Criterion 2: every step is reachable ────────────────────────

        [Test]
        public async Task AGeneratedScroll_PlacesEveryStepNearRevealedTerritory()
        {
            // §5B.3's "always achievable" rule. A step beyond the tier's radius would be the
            // 200-mile trip the design explicitly forbids.
            await RevealTerritory();
            await SeedNearbyPois();

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);

            Assert.That(scroll, Is.Not.Null);

            var steps = await DbContext.ClueSteps
                .Where(s => s.ScrollId == scroll!.Id)
                .ToListAsync();

            var cells = await DbContext.RevealedCells
                .Where(r => r.PlayerId == _player.Id)
                .ToListAsync();

            var radius = ClueTierConfig.For(ClueTier.Wandering).SearchRadiusMetres;

            foreach (var step in steps)
            {
                var nearest = cells.Min(c =>
                {
                    var (south, west, north, east) = FogService.CellBounds(c.GridLat, c.GridLng);

                    return TraceValidator.HaversineMetres(
                        (south + north) / 2, (west + east) / 2,
                        step.TargetLat!.Value, step.TargetLng!.Value);
                });

                Assert.That(nearest, Is.LessThanOrEqualTo(radius * 1.5),
                    $"step {step.StepIndex} is {nearest:F0}m from any revealed cell");
            }
        }

        [Test]
        public async Task AScrollHasItsTiersStepCount()
        {
            await RevealTerritory();
            await SeedNearbyPois();

            foreach (var tier in Enum.GetValues<ClueTier>())
            {
                var scroll = await _sut.GenerateScroll(_player.Id, tier, Ct);

                Assert.That(scroll!.StepCount,
                    Is.EqualTo(ClueTierConfig.For(tier).StepCount),
                    $"{tier} has the wrong step count");
            }
        }

        [Test]
        public async Task WithNoTerritory_NoScrollIsGenerated()
        {
            // Better a clear "not yet" than a scroll whose steps sit somewhere the player has
            // never been.
            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);

            Assert.That(scroll, Is.Null);
        }

        // ── Criterion 9: a rural player still gets a completable scroll ──

        [Test]
        public async Task WithNoPoisNearby_AScrollIsStillGenerated()
        {
            // The rural fixture. §5B.1 wants "a rural and an urban player both get workable
            // clues", so sparse POIs must fall back rather than fail.
            await RevealTerritory();

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);

            Assert.That(scroll, Is.Not.Null);

            var steps = await DbContext.ClueSteps.Where(s => s.ScrollId == scroll!.Id).ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(steps, Has.Count.EqualTo(2));
                Assert.That(steps.All(s => s.StepType == ClueStepType.Coordinate), Is.True,
                    "with no POIs, every step should fall back to the player's own ground");
                Assert.That(steps.All(s => s.TargetLat is not null), Is.True,
                    "and every step must still be completable");
            });
        }

        [Test]
        public async Task ARuralScroll_CanBeCompletedEndToEnd()
        {
            await RevealTerritory();

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);

            for (var i = 0; i < scroll!.StepCount; i++)
            {
                await StandAtCurrentStep(scroll.Id);
                await _sut.AttemptStep(_player.Id, scroll.Id, Ct);
            }

            var completed = await DbContext.PlayerClueScrolls.FirstAsync(s => s.Id == scroll.Id);

            Assert.That(completed.CompletedUtc, Is.Not.Null);
        }

        // ── Criteria 3 & 4: arrival is validated server-side ────────────

        [Test]
        public async Task AStepCompletes_WhenThePlayerIsGenuinelyThere()
        {
            await RevealTerritory();
            await SeedNearbyPois();

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);
            await StandAtCurrentStep(scroll!.Id);

            var progress = await _sut.AttemptStep(_player.Id, scroll.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(progress.StepSolved, Is.True);
                Assert.That(progress.NextStep, Is.Not.Null, "a two-step scroll should advance");
            });
        }

        [Test]
        public async Task AnAttemptFromOutOfRange_IsRejected()
        {
            await RevealTerritory();
            await SeedNearbyPois();

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);

            // Several kilometres away.
            await SetPosition(OriginLat + 0.05, OriginLng + 0.05);

            await Assert.ThatAsync(
                () => _sut.AttemptStep(_player.Id, scroll!.Id, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task AFailedAttempt_DoesNotAdvanceTheScroll()
        {
            await RevealTerritory();
            await SeedNearbyPois();

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);
            await SetPosition(OriginLat + 0.05, OriginLng + 0.05);

            try { await _sut.AttemptStep(_player.Id, scroll!.Id, Ct); } catch (BadRequestException) { }

            var after = await DbContext.PlayerClueScrolls.FirstAsync(s => s.Id == scroll!.Id);

            Assert.That(after.CurrentStep, Is.Zero);
        }

        [Test]
        public async Task AnAttemptWithNoVerifiedPosition_IsRejected()
        {
            // The same hole Stage 04 had: a seeded lat/lng the server never verified must not
            // count as being somewhere.
            await RevealTerritory();
            await SeedNearbyPois();

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);

            await Assert.ThatAsync(
                () => _sut.AttemptStep(_player.Id, scroll!.Id, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        // ── Criterion 8: one active scroll per tier ─────────────────────

        [Test]
        public async Task OnlyOneActiveScrollPerTier()
        {
            await RevealTerritory();
            await SeedNearbyPois();

            await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);

            await Assert.ThatAsync(
                () => _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task DifferentTiers_CanBeHeldAtOnce()
        {
            await RevealTerritory();
            await SeedNearbyPois();

            await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);
            var second = await _sut.GenerateScroll(_player.Id, ClueTier.Roaming, Ct);

            Assert.That(second, Is.Not.Null, "the limit is per tier, not overall");
        }

        [Test]
        public async Task CompletingAScroll_FreesTheTier()
        {
            await RevealTerritory();

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);

            for (var i = 0; i < scroll!.StepCount; i++)
            {
                await StandAtCurrentStep(scroll.Id);
                await _sut.AttemptStep(_player.Id, scroll.Id, Ct);
            }

            var next = await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);

            Assert.That(next, Is.Not.Null);
        }

        // ── Criterion 6: one skip per scroll, at a cost ─────────────────

        [Test]
        public async Task SkippingAdvancesTheStepAndDeductsTheCost()
        {
            await RevealTerritory();
            await GiveCuration(100);

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);

            var before = (await DbContext.Players.FirstAsync(p => p.Id == _player.Id)).Curation;

            var progress = await _sut.SkipStep(_player.Id, scroll!.Id, Ct);

            var after = (await DbContext.Players.FirstAsync(p => p.Id == _player.Id)).Curation;

            Assert.Multiple(() =>
            {
                Assert.That(progress.StepSolved, Is.True);
                Assert.That(after, Is.EqualTo(before - ClueTierConfig.For(ClueTier.Wandering).SkipCostCuration));
            });
        }

        [Test]
        public async Task OnlyOneSkipPerScroll()
        {
            await RevealTerritory();
            await GiveCuration(100);

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Roaming, Ct);

            await _sut.SkipStep(_player.Id, scroll!.Id, Ct);

            await Assert.ThatAsync(
                () => _sut.SkipStep(_player.Id, scroll.Id, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task SkippingWithoutCuration_IsRejected()
        {
            await RevealTerritory();

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);

            await Assert.ThatAsync(
                () => _sut.SkipStep(_player.Id, scroll!.Id, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task ASkippedStep_IsMarkedAsSkippedNotSolved()
        {
            await RevealTerritory();
            await GiveCuration(100);

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);
            await _sut.SkipStep(_player.Id, scroll!.Id, Ct);

            var step = await DbContext.ClueSteps
                .FirstAsync(s => s.ScrollId == scroll.Id && s.StepIndex == 0);

            Assert.That(step.WasSkipped, Is.True);
        }

        // ── Criterion 7: rewards, and the Museum link ───────────────────

        [Test]
        public async Task CompletingAScroll_AwardsARelicToTheMuseum()
        {
            // §5B.4: clues generate Relics, Relics fill the Museum, and neither system carries
            // its weight alone.
            await RevealTerritory();

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);

            ClueProgressDtoHolder holder = new();

            for (var i = 0; i < scroll!.StepCount; i++)
            {
                await StandAtCurrentStep(scroll.Id);
                holder.Last = await _sut.AttemptStep(_player.Id, scroll.Id, Ct);
            }

            Assert.Multiple(() =>
            {
                Assert.That(holder.Last!.ScrollComplete, Is.True);
                Assert.That(holder.Last.Reward, Is.Not.Null);
                Assert.That(holder.Last.Reward!.Relics, Is.Not.Empty, "a scroll should yield a Relic");
                Assert.That(holder.Last.Reward.CurationEarned, Is.GreaterThan(0));
            });
        }

        private sealed class ClueProgressDtoHolder
        {
            public Domain.DTOs.Clues.Responses.ClueProgressDto? Last { get; set; }
        }

        [Test]
        public async Task AwardedRelics_HaveAMuseumPlinth()
        {
            // A Relic key with no plinth would be awarded and then silently vanish.
            var definitions = await DbContext.MuseumEntryDefinitions
                .Where(d => d.Wing == MuseumWing.Relics)
                .Select(d => d.Key)
                .ToListAsync();

            foreach (var (_, key, _) in ClueService.AllRelics())
                Assert.That(definitions, Contains.Item(key), $"{key} has no Museum plinth");
        }

        [Test]
        public async Task HigherTiers_PayMoreCuration()
        {
            var wandering = ClueTierConfig.For(ClueTier.Wandering).CurationReward;
            var odyssey = ClueTierConfig.For(ClueTier.Odyssey).CurationReward;

            Assert.That(odyssey, Is.GreaterThan(wandering));

            await Task.CompletedTask;
        }

        // ── Raw power stays low (§5B.3) ─────────────────────────────────

        [Test]
        public async Task AClueReward_GrantsNoXp()
        {
            // §5B.3: "a player who ignores clues entirely should not fall behind". XP in a
            // reward table would make clues mandatory for optimal play.
            await RevealTerritory();

            var before = await DbContext.Players
                .Where(p => p.Id == _player.Id)
                .Select(p => p.AdventurerXp)
                .FirstAsync();

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Odyssey, Ct);

            for (var i = 0; i < scroll!.StepCount; i++)
            {
                await StandAtCurrentStep(scroll.Id);
                await _sut.AttemptStep(_player.Id, scroll.Id, Ct);
            }

            var after = await DbContext.Players
                .Where(p => p.Id == _player.Id)
                .Select(p => p.AdventurerXp)
                .FirstAsync();

            Assert.That(after, Is.EqualTo(before), "clues must not be an XP route");
        }

        // ── Future steps are withheld ───────────────────────────────────

        [Test]
        public async Task FutureSteps_AreNotRevealed()
        {
            // Showing the whole chain would let a player plan a route the clue is meant to
            // reveal one leg at a time.
            await RevealTerritory();
            await SeedNearbyPois();

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Odyssey, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(scroll!.StepCount, Is.EqualTo(5));
                Assert.That(scroll.Steps, Has.Count.EqualTo(1), "only the current step should be visible");
            });
        }

        [Test]
        public async Task ANonCoordinateStep_DoesNotLeakItsPosition()
        {
            // A Direct step names its POI in the riddle; handing over the coordinates too
            // would turn the walk into a map pin.
            await RevealTerritory();
            await SeedNearbyPois();

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);
            var step = scroll!.Steps[0];

            if (step.StepType != ClueStepType.Coordinate)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(step.SearchLat, Is.Null);
                    Assert.That(step.SearchLng, Is.Null);
                });
            }
        }

        [Test]
        public async Task ACoordinateStep_ExposesOnlyASearchArea()
        {
            await RevealTerritory();

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);
            var step = scroll!.Steps[0];

            Assert.Multiple(() =>
            {
                Assert.That(step.StepType, Is.EqualTo(ClueStepType.Coordinate));
                Assert.That(step.SearchRadius, Is.GreaterThan(0), "the player needs to know how much ground to cover");
            });
        }

        [Test]
        public async Task AnotherPlayersScroll_CannotBeAttempted()
        {
            await RevealTerritory();

            var scroll = await _sut.GenerateScroll(_player.Id, ClueTier.Wandering, Ct);

            var otherUser = await TestDatabaseSeedHelper.SeedTestUser(DbContext, "clue_intruder");
            var other = await TestDatabaseSeedHelper.SeedTestPlayer(DbContext, otherUser);

            await Assert.ThatAsync(
                () => _sut.AttemptStep(other.Id, scroll!.Id, Ct),
                Throws.TypeOf<NotFoundException>());
        }

        // ── Riddle text ─────────────────────────────────────────────────

        [Test]
        public void RiddleText_IsDeterministic()
        {
            // Regenerating a scroll must not reword a step the player is already carrying.
            var first = ClueRiddleText.PhraseFor(SkillType.Prayer, 12345);
            var second = ClueRiddleText.PhraseFor(SkillType.Prayer, 12345);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void EverySkill_HasRiddlePhrasing()
        {
            foreach (var skill in Enum.GetValues<SkillType>())
            {
                var phrase = ClueRiddleText.PhraseFor(skill, 1);

                Assert.That(phrase, Is.Not.Empty, $"{skill} has no phrasing");
            }
        }

        [Test]
        public void ACategoryRiddle_NeverNamesAPoi()
        {
            // A Category step means "any lighthouse" — naming one would make it a Direct step
            // with the wrong validation.
            var text = ClueRiddleText.Category(SkillType.Prayer, 1);

            Assert.That(text, Does.Not.Contain("Test Place"));
            Assert.That(text, Does.StartWith("Stand somewhere"));
        }
    }
}
