using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Fog;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Domain.Services.Skills;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Tests.Services.Skills
{
    /// <summary>
    /// The two skill synergies Stage 15 described but did not build: Athletics' distance
    /// synergy, and Banking-driven stack caps.
    ///
    /// <para>Both existed as skills that levelled without moving anything a player could
    /// feel. §4.3's rule that an equipped item changing no behaviour is a bug applies to a
    /// skill just as well, and these are the readers that make the levels mean something.</para>
    /// </summary>
    [TestFixture]
    public class SkillSynergyTests : DatabaseIntegrationTestBase
    {
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

            _progression = TestDatabaseSeedHelper.CreateProgressionService(DbContext);
            await _progression.EnsureStartingUnlocks(_player.Id, Ct);
        }

        private async Task Unlock(SkillType skill, int level = 1)
        {
            var existing = await DbContext.PlayerSkills
                .FirstOrDefaultAsync(s => s.PlayerId == _player.Id && s.SkillType == skill);

            if (existing is null)
            {
                existing = new PlayerSkill
                {
                    PlayerId = _player.Id,
                    SkillType = skill,
                    UnlockedAtUtc = DateTime.UtcNow,
                };
                DbContext.PlayerSkills.Add(existing);
            }

            existing.Level = level;
            existing.Xp = XpCurve.XpForLevel(level);
            await DbContext.SaveChangesAsync();
        }

        private SkillTrainingService TrainingService(TerrainType terrain)
        {
            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, terrain);
            return TestDatabaseSeedHelper.CreateSkillTrainingService(DbContext, _progression, materials);
        }

        private async Task<long> AthleticsXp()
        {
            var skill = await DbContext.PlayerSkills
                .AsNoTracking()
                .FirstAsync(s => s.PlayerId == _player.Id && s.SkillType == SkillType.Athletics);

            return skill.Xp;
        }

        // ── Athletics: distance synergy ─────────────────────────────────

        [Test]
        public async Task WalkingDistance_TrainsAthleticsBeyondTheCells()
        {
            await Unlock(SkillType.Athletics);

            var sut = TrainingService(TerrainType.Urban);

            var withoutDistance = await sut.TrainFromCells(_player.Id, [new GridCell(10, 10)], 0, Ct);
            var cellsOnly = withoutDistance.First(r => r.SkillType == SkillType.Athletics).SkillXpEarned;

            var withDistance = await sut.TrainFromCells(_player.Id, [new GridCell(11, 10)], 2000, Ct);
            var withKilometres = withDistance.First(r => r.SkillType == SkillType.Athletics).SkillXpEarned;

            Assert.That(withKilometres, Is.GreaterThan(cellsOnly),
                "two kilometres walked must be worth more than the same cell count alone");
        }

        [Test]
        public async Task ARewalkedRoute_StillTrainsAthletics()
        {
            // The case the feature exists for. A lap of a route already walked reveals no new
            // cells, so before this every other skill correctly earned nothing — and so did
            // the skill named after endurance, which is backwards.
            await Unlock(SkillType.Athletics);

            var before = await AthleticsXp();

            await TrainingService(TerrainType.Urban).TrainFromCells(_player.Id, [], 5000, Ct);

            Assert.That(await AthleticsXp(), Is.GreaterThan(before),
                "distance walked must pay even when no new ground was revealed");
        }

        [Test]
        public async Task DistanceDoesNotTrainOtherSkills()
        {
            // Distance is Athletics' own synergy. If it leaked into every unlocked skill it
            // would be a flat XP multiplier on walking, which is not what it is for.
            await Unlock(SkillType.Athletics);
            await Unlock(SkillType.Foraging);

            var sut = TrainingService(TerrainType.Urban);

            var withoutDistance = await sut.TrainFromCells(_player.Id, [new GridCell(10, 10)], 0, Ct);
            var withDistance = await sut.TrainFromCells(_player.Id, [new GridCell(11, 10)], 5000, Ct);

            var foragingBefore = withoutDistance.First(r => r.SkillType == SkillType.Foraging).SkillXpEarned;
            var foragingAfter = withDistance.First(r => r.SkillType == SkillType.Foraging).SkillXpEarned;

            Assert.That(foragingAfter, Is.EqualTo(foragingBefore));
        }

        [Test]
        public async Task ALockedAthletics_EarnsNothingFromDistance()
        {
            // The row existing is the unlock (§3.1). Distance must not be a back door around
            // the ladder.
            var locked = await DbContext.PlayerSkills
                .AnyAsync(s => s.PlayerId == _player.Id && s.SkillType == SkillType.Athletics);

            Assert.That(locked, Is.False, "Athletics should not be unlocked at the start");

            var results = await TrainingService(TerrainType.Urban)
                .TrainFromCells(_player.Id, [], 5000, Ct);

            Assert.That(results.Any(r => r.SkillType == SkillType.Athletics), Is.False);
        }

        [Test]
        public async Task DistanceSynergy_IsASupplementNotAShortcut()
        {
            // §4.1a's accessibility rule cuts both ways: geography must not disqualify
            // anyone, and distance must not out-earn actually exploring. An hour's brisk walk
            // is ~5 km, which should pay well under what the same hour's new cells do.
            await Unlock(SkillType.Athletics);

            var sut = TrainingService(TerrainType.Urban);

            var distanceOnly = await sut.TrainFromCells(_player.Id, [], 5000, Ct);
            var fromDistance = distanceOnly.First(r => r.SkillType == SkillType.Athletics).SkillXpEarned;

            var cells = Enumerable.Range(0, 60).Select(i => new GridCell(100 + i, 100)).ToList();
            var cellRun = await sut.TrainFromCells(_player.Id, cells, 0, Ct);
            var fromCells = cellRun.First(r => r.SkillType == SkillType.Athletics).SkillXpEarned;

            Assert.That(fromDistance, Is.LessThan(fromCells),
                "covering new ground must remain the better route");
        }

        // ── Banking: stack caps ─────────────────────────────────────────

        /// <summary>The effective cap the inventory reports for the player's first material.</summary>
        private async Task<int> FirstStackCap()
        {
            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Urban);
            var inventory = await materials.GetInventory(_player.Id, Ct);

            return inventory.Categories.SelectMany(c => c.Items).First().StackCap;
        }

        [Test]
        public async Task BankingLevel_RaisesStackCaps()
        {
            var material = await DbContext.Materials.FirstAsync();
            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Urban);

            await materials.GrantMaterials(_player.Id, new Dictionary<int, int> { [material.Id] = 1 }, Ct);

            await Unlock(SkillType.Banking, 1);
            var atLevelOne = await FirstStackCap();

            await Unlock(SkillType.Banking, 99);
            var atLevelNinetyNine = await FirstStackCap();

            Assert.That(atLevelNinetyNine, Is.GreaterThan(atLevelOne),
                "Banking is about what you can keep, so levelling it must change what you can keep");
        }

        [Test]
        public async Task OtherSkillLevels_DoNotRaiseStackCaps()
        {
            var material = await DbContext.Materials.FirstAsync();
            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Urban);

            await materials.GrantMaterials(_player.Id, new Dictionary<int, int> { [material.Id] = 1 }, Ct);

            var before = await FirstStackCap();

            await Unlock(SkillType.Foraging, 99);

            Assert.That(await FirstStackCap(), Is.EqualTo(before));
        }
    }
}
