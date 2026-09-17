using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Crafting;
using GeoSlayer.Domain.Services.Fog;
using GeoSlayer.Domain.Services.Idle;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Domain.Services.Skills;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Tests.Services.Skills
{
    /// <summary>
    /// Stage 07 criteria 2, 3, 4, 8 and 9 — Fishing end to end, against a real database.
    ///
    /// <para>The tier and geography criteria (5, 6, 7) are covered for every skill at once by
    /// <see cref="GatheringSkillLadderTests"/>, so they are not duplicated here.</para>
    /// </summary>
    [TestFixture]
    public class FishingIntegrationTests : DatabaseIntegrationTestBase
    {
        private ProgressionService _progression = null!;
        private CraftingService _crafting = null!;
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

            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext);
            _crafting = TestDatabaseSeedHelper.CreateCraftingService(DbContext, _progression, materials);
        }

        private SkillTrainingService TrainingFor(TerrainType terrain)
        {
            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, terrain);
            return TestDatabaseSeedHelper.CreateSkillTrainingService(DbContext, _progression, materials);
        }

        private async Task<bool> HasFishing() =>
            await DbContext.PlayerSkills
                .AnyAsync(s => s.PlayerId == _player.Id && s.SkillType == SkillType.Fishing);

        // ── Criterion 2: unlocks at Adventurer 3, not before ────────────

        [Test]
        public async Task Fishing_IsNotUnlockedAtLevelOne()
        {
            Assert.That(await HasFishing(), Is.False, "Fishing arrives at Adventurer 3, not 1");
        }

        [Test]
        public async Task Fishing_UnlocksAtAdventurerLevelThree()
        {
            // Enough Adventurer XP to cross level 3.
            var target = XpCurve.XpForLevel(3);
            var result = await _progression.GrantXp(_player.Id, null, target * 4, XpSource.Walk, Ct);

            Assert.Multiple(async () =>
            {
                Assert.That(await HasFishing(), Is.True);
                Assert.That(result.Unlocks.Any(u => u.Payload == nameof(SkillType.Fishing)), Is.True,
                    "the celebration needs the unlock event");
            });
        }

        [Test]
        public async Task Fishing_HasASeededDefinitionForTheSkillsScreen()
        {
            var definition = await DbContext.SkillDefinitions
                .FirstOrDefaultAsync(d => d.SkillType == SkillType.Fishing);

            Assert.Multiple(() =>
            {
                Assert.That(definition, Is.Not.Null);
                Assert.That(definition!.UnlockLevel, Is.EqualTo(3));
                Assert.That(definition.Category, Is.EqualTo(SkillCategory.Gathering));
            });
        }

        // ── Criteria 3 & 4: the three training routes ───────────────────

        [Test]
        public async Task WalkingWater_TrainsFishingWell()
        {
            await UnlockFishing();

            var results = await TrainingFor(TerrainType.Water)
                .TrainFromCells(_player.Id, [new GridCell(10, 10), new GridCell(11, 10)], 0, Ct);

            var fishing = results.FirstOrDefault(r => r.SkillType == SkillType.Fishing);

            Assert.Multiple(() =>
            {
                Assert.That(fishing, Is.Not.Null);
                Assert.That(fishing!.SkillXpEarned, Is.EqualTo(6), "water is 3 XP/cell across 2 cells");
            });
        }

        [Test]
        public async Task WalkingInland_StillTrainsFishingAtBaseRate()
        {
            // Criterion 4, and the point of the whole geography rule: a landlocked player
            // must still be able to train Fishing.
            await UnlockFishing();

            var results = await TrainingFor(TerrainType.Rocky)
                .TrainFromCells(_player.Id, [new GridCell(20, 20)], 0, Ct);

            var fishing = results.FirstOrDefault(r => r.SkillType == SkillType.Fishing);

            Assert.Multiple(() =>
            {
                Assert.That(fishing, Is.Not.Null, "rocky ground must still train Fishing");
                Assert.That(fishing!.SkillXpEarned, Is.EqualTo(1), "at base rate");
            });
        }

        [Test]
        public async Task WaterOutpacesInland_ButBothTrain()
        {
            await UnlockFishing();

            var inland = await TrainingFor(TerrainType.Urban)
                .TrainFromCells(_player.Id, [new GridCell(30, 30)], 0, Ct);

            var water = await TrainingFor(TerrainType.Water)
                .TrainFromCells(_player.Id, [new GridCell(31, 30)], 0, Ct);

            var inlandXp = inland.First(r => r.SkillType == SkillType.Fishing).SkillXpEarned;
            var waterXp = water.First(r => r.SkillType == SkillType.Fishing).SkillXpEarned;

            Assert.That(waterXp, Is.GreaterThan(inlandXp),
                "terrain should matter for speed without gating access");
        }

        [Test]
        public async Task TheAdventurerCut_IsTheConfiguredRatio()
        {
            await UnlockFishing();

            var results = await TrainingFor(TerrainType.Water)
                .TrainFromCells(
                    _player.Id,
                    Enumerable.Range(0, 10).Select(i => new GridCell(100 + i, 100)).ToList(),
                    0,
                    Ct);

            foreach (var result in results)
            {
                Assert.That(result.AdventurerXpEarned,
                    Is.EqualTo((long)Math.Floor(result.SkillXpEarned * ProgressionDefaults.GlobalXpRatio)),
                    $"{result.Name} paid the wrong Adventurer cut");
            }
        }

        // ── Criterion 8: materials reach the inventory ──────────────────

        [Test]
        public async Task FishingYieldsItsOwnMaterials()
        {
            await UnlockFishing();

            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Water);

            var gains = await materials.AwardCellDrops(
                _player.Id,
                Enumerable.Range(0, 20).Select(i => new GridCell(200 + i, 200)).ToList(),
                Ct);

            var fishingKeys = SkillSeedData.FishingMaterials.Select(m => m.Key).ToHashSet();

            Assert.That(gains.Any(g => fishingKeys.Contains(g.Key)), Is.True,
                "walking water with Fishing unlocked should yield fish");
        }

        [Test]
        public async Task FishMaterialsAppearInInventoryWithCaps()
        {
            await UnlockFishing();

            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext);
            var minnow = await DbContext.Materials.FirstAsync(m => m.Key == "minnow");

            await materials.GrantMaterials(_player.Id, new Dictionary<int, int> { [minnow.Id] = 5 }, Ct);

            var inventory = await materials.GetInventory(_player.Id, Ct);
            var item = inventory.Categories.SelectMany(c => c.Items).First(i => i.Key == "minnow");

            Assert.Multiple(() =>
            {
                Assert.That(item.Quantity, Is.EqualTo(5));
                Assert.That(item.StackCap, Is.GreaterThan(0));
            });
        }

        // ── Criterion 9: a worker can fish ──────────────────────────────

        [Test]
        public async Task AWorkerCanBeAssignedToFishing_AndProduces()
        {
            await UnlockFishing();

            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Water);
            var workers = new WorkerService(DbContext, _progression, materials, _crafting);

            // Reveal a block and claim it.
            var now = DateTime.UtcNow;

            for (var lat = 299; lat <= 301; lat++)
            {
                for (var lng = 299; lng <= 301; lng++)
                {
                    DbContext.RevealedCells.Add(new RevealedCell
                    {
                        PlayerId = _player.Id,
                        GridLat = lat,
                        GridLng = lng,
                        RevealedAtUtc = now,
                    });
                }
            }

            for (var i = 0; i < 60; i++)
            {
                DbContext.RevealedCells.Add(new RevealedCell
                {
                    PlayerId = _player.Id,
                    GridLat = 800 + i,
                    GridLng = 800,
                    RevealedAtUtc = now,
                });
            }

            await DbContext.SaveChangesAsync();

            foreach (var key in new[] { "wild_grass", "scrap" })
            {
                var material = await DbContext.Materials.FirstAsync(m => m.Key == key);

                var row = await DbContext.PlayerMaterials
                    .FirstOrDefaultAsync(pm => pm.PlayerId == _player.Id && pm.MaterialId == material.Id);

                if (row is null)
                {
                    row = new PlayerMaterial { PlayerId = _player.Id, MaterialId = material.Id };
                    DbContext.PlayerMaterials.Add(row);
                }

                row.Quantity += 100;
            }

            await DbContext.SaveChangesAsync();

            var claim = await workers.CreateClaim(_player.Id, 300, 300, "Riverside", Ct);
            var worker = await workers.HireWorker(_player.Id, Ct);

            // Any unlocked skill on any Claim — no terrain check on the assignment (§5.2).
            var assigned = await workers.AssignWorker(
                _player.Id, worker.Id, claim.Id, SkillType.Fishing, Ct);

            // Simulate an absence.
            var row2 = await DbContext.Workers.FirstAsync(w => w.Id == worker.Id);
            row2.LastCollectedAtUtc -= TimeSpan.FromHours(3);
            await DbContext.SaveChangesAsync();

            var accrual = await workers.CollectOfflineAccrual(_player.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(assigned.AssignedSkill, Is.EqualTo(SkillType.Fishing));
                Assert.That(accrual.Skills.Any(s => s.SkillType == SkillType.Fishing), Is.True,
                    "a worker assigned to Fishing should produce Fishing XP");
                Assert.That(accrual.Materials, Is.Not.Empty, "and materials");
            });
        }

        /// <summary>Push the player to Adventurer 3, which grants Fishing.</summary>
        private async Task UnlockFishing()
        {
            var target = XpCurve.XpForLevel(3);
            await _progression.GrantXp(_player.Id, null, target * 4, XpSource.Walk, Ct);
        }
    }
}
