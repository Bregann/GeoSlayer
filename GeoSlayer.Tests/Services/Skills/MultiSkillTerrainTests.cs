using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Fog;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Domain.Services.Skills;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Tests.Services.Skills
{
    /// <summary>
    /// Stage 08's stage note: a woodland cell trains <b>both</b> Foraging and Woodcutting,
    /// and each must roll its own tier independently.
    ///
    /// <para>This is the first case where one cell feeds two skills. Getting it wrong would
    /// most likely show up as one skill silently shadowing the other — either taking all the
    /// XP, or its ladder crowding the other out of the drop roll.</para>
    /// </summary>
    [TestFixture]
    public class MultiSkillTerrainTests : DatabaseIntegrationTestBase
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

        /// <summary>Unlock a skill directly, without walking to the Adventurer level.</summary>
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

        // ── XP: one cell, two skills ────────────────────────────────────

        [Test]
        public async Task AWoodlandCell_TrainsBothForagingAndWoodcutting()
        {
            await Unlock(SkillType.Woodcutting);

            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Woodland);
            var training = TestDatabaseSeedHelper.CreateSkillTrainingService(DbContext, _progression, materials);

            var results = await training.TrainFromCells(_player.Id, [new GridCell(10, 10)], 0, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(results.Any(r => r.SkillType == SkillType.Foraging), Is.True,
                    "woodland should still train Foraging");
                Assert.That(results.Any(r => r.SkillType == SkillType.Woodcutting), Is.True,
                    "and Woodcutting alongside it");
            });
        }

        [Test]
        public async Task BothSkills_EarnTheirOwnRateNotAShare()
        {
            // The XP must not be split between them — each skill's mapping is its own rate,
            // so one cell of woodland pays 3 to each rather than 1.5.
            await Unlock(SkillType.Woodcutting);

            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Woodland);
            var training = TestDatabaseSeedHelper.CreateSkillTrainingService(DbContext, _progression, materials);

            var results = await training.TrainFromCells(_player.Id, [new GridCell(20, 20)], 0, Ct);

            var foraging = results.First(r => r.SkillType == SkillType.Foraging);
            var woodcutting = results.First(r => r.SkillType == SkillType.Woodcutting);

            Assert.Multiple(() =>
            {
                Assert.That(foraging.SkillXpEarned, Is.EqualTo(3));
                Assert.That(woodcutting.SkillXpEarned, Is.EqualTo(3));
            });
        }

        [Test]
        public async Task AddingASecondSkill_DoesNotReduceTheFirst()
        {
            // Regression guard: unlocking Woodcutting must not cost the player Foraging XP.
            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Woodland);
            var training = TestDatabaseSeedHelper.CreateSkillTrainingService(DbContext, _progression, materials);

            var before = await training.TrainFromCells(_player.Id, [new GridCell(30, 30)], 0, Ct);
            var foragingAlone = before.First(r => r.SkillType == SkillType.Foraging).SkillXpEarned;

            await Unlock(SkillType.Woodcutting);

            var after = await training.TrainFromCells(_player.Id, [new GridCell(31, 30)], 0, Ct);
            var foragingWith = after.First(r => r.SkillType == SkillType.Foraging).SkillXpEarned;

            Assert.That(foragingWith, Is.EqualTo(foragingAlone),
                "a second skill on the same terrain must not dilute the first");
        }

        [Test]
        public async Task ALockedSecondSkill_DoesNotTrain()
        {
            // Woodcutting is not unlocked at Adventurer 1, so a woodland cell trains only
            // Foraging until the ladder grants it.
            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Woodland);
            var training = TestDatabaseSeedHelper.CreateSkillTrainingService(DbContext, _progression, materials);

            var results = await training.TrainFromCells(_player.Id, [new GridCell(40, 40)], 0, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(results.Any(r => r.SkillType == SkillType.Foraging), Is.True);
                Assert.That(results.Any(r => r.SkillType == SkillType.Woodcutting), Is.False);
            });
        }

        // ── Drops: each skill rolls its own tier ────────────────────────

        [Test]
        public async Task EachSkillRollsItsOwnLadder_OnTheSameCell()
        {
            // The stage note's real concern: two ladders on one terrain must not crowd each
            // other out. Both categories should appear across a walk.
            await Unlock(SkillType.Woodcutting);

            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Woodland);

            var gains = await materials.AwardCellDrops(
                _player.Id,
                Enumerable.Range(0, 40).Select(i => new GridCell(100 + i, 100)).ToList(),
                Ct);

            var foragedKeys = SkillSeedData.ForagingMaterials.Select(m => m.Key).ToHashSet();
            var loggedKeys = SkillSeedData.WoodcuttingMaterials.Select(m => m.Key).ToHashSet();

            Assert.Multiple(() =>
            {
                Assert.That(gains.Any(g => foragedKeys.Contains(g.Key)), Is.True,
                    "Foraging materials should drop on woodland");
                Assert.That(gains.Any(g => loggedKeys.Contains(g.Key)), Is.True,
                    "and Woodcutting materials too — neither ladder should shadow the other");
            });
        }

        [Test]
        public async Task EachSkillsTierRespectsItsOwnLevel()
        {
            // Independent tiers: high Woodcutting must not drag Foraging's tier up with it,
            // nor the reverse. This is what "each rolls its own tier" actually means.
            await Unlock(SkillType.Foraging, 1);
            await Unlock(SkillType.Woodcutting, 20);

            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Woodland);

            var gains = await materials.AwardCellDrops(
                _player.Id,
                Enumerable.Range(0, 60).Select(i => new GridCell(200 + i, 200)).ToList(),
                Ct);

            var gainedKeys = gains.Select(g => g.Key).ToHashSet();

            // Foraging is level 1, so its tier-2+ materials (level 10 and up) are unreachable.
            var forbidden = SkillSeedData.ForagingMaterials
                .Where(m => m.LevelRequired > 1)
                .Select(m => m.Key);

            foreach (var key in forbidden)
            {
                Assert.That(gainedKeys.Contains(key), Is.False,
                    $"{key} needs Foraging {SkillSeedData.ForagingMaterials.First(m => m.Key == key).LevelRequired}, but Foraging is 1");
            }
        }

        [Test]
        public async Task AWorkerOnWoodland_TrainsWhicheverSkillItIsAssigned()
        {
            // Terrain suiting two skills must not make the worker's assignment ambiguous —
            // the worker trains what it was told to, and gets the terrain bonus for it.
            await Unlock(SkillType.Woodcutting);

            var claimTerrain = TerrainType.Woodland;

            var foragingTerrains = SkillSeedData.TerrainMappings
                .Where(m => m.SkillType == SkillType.Foraging && m.Terrain != TerrainType.Open)
                .Where(m => m.XpPerCell > 1.0)
                .Aggregate(TerrainType.Open, (acc, m) => acc | m.Terrain);

            var woodcuttingTerrains = SkillSeedData.TerrainMappings
                .Where(m => m.SkillType == SkillType.Woodcutting && m.Terrain != TerrainType.Open)
                .Where(m => m.XpPerCell > 1.0)
                .Aggregate(TerrainType.Open, (acc, m) => acc | m.Terrain);

            Assert.Multiple(() =>
            {
                Assert.That(
                    Domain.Services.Idle.OfflineAccrual.TerrainMatches(claimTerrain, foragingTerrains),
                    Is.True, "woodland should suit Foraging");

                Assert.That(
                    Domain.Services.Idle.OfflineAccrual.TerrainMatches(claimTerrain, woodcuttingTerrains),
                    Is.True, "and Woodcutting — one Claim can be good for both");
            });
        }
    }
}
