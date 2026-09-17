using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Interfaces.Api.Materials;
using GeoSlayer.Domain.Services.Fog;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GeoSlayer.Tests.Services.Materials
{
    /// <summary>
    /// Stage 03 criteria 3, 4, 5, 6, 7 and 8, against a real database.
    ///
    /// Terrain is supplied by a fake classifier throughout — no test may reach Overpass.
    /// </summary>
    [TestFixture]
    public class MaterialServiceIntegrationTests : DatabaseIntegrationTestBase
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

            _progression = TestDatabaseSeedHelper.CreateProgressionService(DbContext);
            await _progression.EnsureStartingUnlocks(_player.Id, Ct);
        }

        private MaterialService ServiceFor(TerrainType terrain) =>
            TestDatabaseSeedHelper.CreateMaterialService(DbContext, terrain);

        private async Task<int> MaterialId(string key) =>
            await DbContext.Materials.Where(m => m.Key == key).Select(m => m.Id).FirstAsync();

        /// <summary>Unlock a gathering skill at a level, so its tiers become reachable.</summary>
        private async Task SetSkill(SkillType skill, int level)
        {
            var row = await DbContext.PlayerSkills
                .FirstOrDefaultAsync(s => s.PlayerId == _player.Id && s.SkillType == skill);

            if (row is null)
            {
                row = new PlayerSkill
                {
                    PlayerId = _player.Id,
                    SkillType = skill,
                    UnlockedAtUtc = DateTime.UtcNow,
                };
                DbContext.PlayerSkills.Add(row);
            }

            row.Level = level;
            row.Xp = XpCurve.XpForLevel(level);
            await DbContext.SaveChangesAsync();
        }

        // ── Criterion 3: terrain selects the pool ───────────────────────

        [Test]
        public async Task AWoodlandCell_YieldsWoodlandMaterials()
        {
            await SetSkill(SkillType.Woodcutting, 1);

            var sut = ServiceFor(TerrainType.Woodland);
            var gains = await sut.AwardCellDrops(_player.Id, [new GridCell(10, 10)], Ct);

            var keys = gains.Select(g => g.Key).ToList();

            Assert.That(keys, Contains.Item("timber_rough"),
                "a woodland cell should draw from the woodland pool");
        }

        [Test]
        public async Task AnUrbanCell_YieldsUrbanMaterials()
        {
            await SetSkill(SkillType.Trading, 1);

            var sut = ServiceFor(TerrainType.Urban);
            var gains = await sut.AwardCellDrops(_player.Id, [new GridCell(20, 20)], Ct);

            Assert.That(gains.Select(g => g.Key), Contains.Item("scrap"));
        }

        // ── Criterion 4: an Open cell still yields ──────────────────────

        [Test]
        public async Task AnUnclassifiedCell_StillYieldsSomething()
        {
            await SetSkill(SkillType.Trading, 1);

            var sut = ServiceFor(TerrainType.Open);
            var gains = await sut.AwardCellDrops(_player.Id, [new GridCell(30, 30)], Ct);

            Assert.That(gains, Is.Not.Empty, "an Open cell must never be a dead zone");
            Assert.That(gains.Sum(g => g.Quantity), Is.GreaterThan(0));
        }

        // ── Criterion 5 (revised): no stack caps ────────────────────────
        //
        // §7.4 originally capped per material, with overflow becoming Dust. Caps were removed
        // when the coin economy landed (§5.4): selling at a shop gives a positive reason to
        // return, where a cap gave a punitive one, and the offline *time* cap still paces the
        // idle layer. These tests hold the new contract — nothing is ever lost.

        [Test]
        public async Task GatheringIntoALargeStack_AcceptsEverything()
        {
            var sut = ServiceFor(TerrainType.Open);
            var scrapId = await MaterialId("scrap");

            DbContext.PlayerMaterials.Add(new PlayerMaterial
            {
                PlayerId = _player.Id,
                MaterialId = scrapId,
                Quantity = 5_000_000,
            });
            await DbContext.SaveChangesAsync();

            var gains = await sut.GrantMaterials(_player.Id, new Dictionary<int, int> { [scrapId] = 10 }, Ct);

            var held = await DbContext.PlayerMaterials
                .Where(pm => pm.PlayerId == _player.Id && pm.MaterialId == scrapId)
                .Select(pm => pm.Quantity)
                .FirstAsync();

            Assert.Multiple(() =>
            {
                Assert.That(gains.First(g => g.MaterialId == scrapId).Quantity, Is.EqualTo(10));
                Assert.That(held, Is.EqualTo(5_000_010), "a big stack is not a full stack");
            });
        }

        [Test]
        public async Task NothingIsEverConvertedToDustOnGather()
        {
            // The regression that matters: a player who gathers a lot must keep all of it.
            // Dust is now just an Open-terrain drop, never a consolation prize for overflow.
            var sut = ServiceFor(TerrainType.Open);
            var scrapId = await MaterialId("scrap");
            var dustId = await MaterialId(MaterialSeedData.DustKey);

            await sut.GrantMaterials(_player.Id, new Dictionary<int, int> { [scrapId] = 500_000 }, Ct);

            var dustHeld = await DbContext.PlayerMaterials
                .Where(pm => pm.PlayerId == _player.Id && pm.MaterialId == dustId)
                .Select(pm => pm.Quantity)
                .FirstOrDefaultAsync();

            var scrapHeld = await DbContext.PlayerMaterials
                .Where(pm => pm.PlayerId == _player.Id && pm.MaterialId == scrapId)
                .Select(pm => pm.Quantity)
                .FirstAsync();

            Assert.Multiple(() =>
            {
                Assert.That(scrapHeld, Is.EqualTo(500_000), "every unit is kept");
                Assert.That(dustHeld, Is.Zero, "gathering must not manufacture Dust");
            });
        }

        [Test]
        public async Task RepeatedGathering_AccumulatesWithoutBound()
        {
            var sut = ServiceFor(TerrainType.Open);
            var scrapId = await MaterialId("scrap");

            for (var i = 0; i < 5; i++)
            {
                await sut.GrantMaterials(_player.Id, new Dictionary<int, int> { [scrapId] = 100_000 }, Ct);
            }

            var held = await DbContext.PlayerMaterials
                .Where(pm => pm.PlayerId == _player.Id && pm.MaterialId == scrapId)
                .Select(pm => pm.Quantity)
                .FirstAsync();

            Assert.That(held, Is.EqualTo(500_000));
        }

        // ── Criterion 6: replay determinism, through the service ────────

        [Test]
        public async Task ReplayingAnIdenticalSync_YieldsIdenticalDrops()
        {
            await SetSkill(SkillType.Woodcutting, 1);

            var cells = new List<GridCell> { new(40, 40), new(41, 40) };

            var first = await ServiceFor(TerrainType.Woodland).AwardCellDrops(_player.Id, cells, Ct);
            var firstTotals = first.ToDictionary(g => g.Key, g => g.Quantity);

            var second = await ServiceFor(TerrainType.Woodland).AwardCellDrops(_player.Id, cells, Ct);
            var secondTotals = second.ToDictionary(g => g.Key, g => g.Quantity);

            Assert.That(secondTotals, Is.EqualTo(firstTotals),
                "a replayed sync must not re-roll for a better result");
        }

        // ── Criterion 8: terrain classification is cached ───────────────

        [Test]
        public async Task TerrainIsClassifiedOnce_AndReusedBySecondPlayer()
        {
            var classifier = new Mock<ITerrainClassifier>();

            classifier
                .Setup(c => c.Classify(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TerrainType.Woodland);

            var sut = new MaterialService(DbContext, classifier.Object);

            var first = await sut.GetOrClassifyTerrain(70, 70, Ct);
            var second = await sut.GetOrClassifyTerrain(70, 70, Ct);

            // A second player revealing the same cell must hit the cache, not the classifier.
            var otherUser = await TestDatabaseSeedHelper.SeedTestUser(DbContext, "second_player");
            await TestDatabaseSeedHelper.SeedTestPlayer(DbContext, otherUser);
            var third = await sut.GetOrClassifyTerrain(70, 70, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.EqualTo(TerrainType.Woodland));
                Assert.That(second, Is.EqualTo(TerrainType.Woodland));
                Assert.That(third, Is.EqualTo(TerrainType.Woodland));
            });

            classifier.Verify(
                c => c.Classify(70, 70, It.IsAny<CancellationToken>()),
                Times.Once,
                "the cell must be classified once and cached for every later reader");
        }

        [Test]
        public async Task CachedTerrain_IsStoredOncePerCell()
        {
            var sut = ServiceFor(TerrainType.Rocky);

            await sut.GetOrClassifyTerrain(80, 80, Ct);
            await sut.GetOrClassifyTerrain(80, 80, Ct);

            var rows = await DbContext.CellTerrains.CountAsync(c => c.GridLat == 80 && c.GridLng == 80);

            Assert.That(rows, Is.EqualTo(1), "the unique index should keep one row per cell");
        }

        // ── Criterion 7: the inventory ──────────────────────────────────

        [Test]
        public async Task Inventory_ListsMaterialsWithQuantitiesAndPrices()
        {
            var sut = ServiceFor(TerrainType.Open);

            var scrapId = await MaterialId("scrap");
            await sut.GrantMaterials(_player.Id, new Dictionary<int, int> { [scrapId] = 5 }, Ct);

            var inventory = await sut.GetInventory(_player.Id, Ct);
            var item = inventory.Categories.SelectMany(c => c.Items).First(i => i.MaterialId == scrapId);

            Assert.Multiple(() =>
            {
                Assert.That(item.Quantity, Is.EqualTo(5));
                Assert.That(item.UnitPrice, Is.GreaterThan(0), "everything is worth something");
                Assert.That(item.StackPrice, Is.EqualTo(item.UnitPrice * 5));
                Assert.That(inventory.DistinctMaterials, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Inventory_ReportsWhatTheWholeHaulIsWorth()
        {
            // The number that decides whether a trip to a shop is worth making, so it has to
            // be on the inventory rather than discovered at the till.
            var sut = ServiceFor(TerrainType.Open);

            var scrapId = await MaterialId("scrap");
            await sut.GrantMaterials(_player.Id, new Dictionary<int, int> { [scrapId] = 10 }, Ct);

            var inventory = await sut.GetInventory(_player.Id, Ct);
            var items = inventory.Categories.SelectMany(c => c.Items).ToList();

            Assert.That(inventory.TotalSellValue, Is.EqualTo(items.Sum(i => i.StackPrice)));
            Assert.That(inventory.TotalSellValue, Is.GreaterThan(0));
        }

        [Test]
        public async Task Inventory_OmitsMaterialsThePlayerDoesNotHold()
        {
            var sut = ServiceFor(TerrainType.Open);

            var inventory = await sut.GetInventory(_player.Id, Ct);

            Assert.That(inventory.DistinctMaterials, Is.Zero,
                "an empty inventory should not list every seeded material at zero");
        }

        [Test]
        public async Task Inventory_GroupsByCategory()
        {
            var sut = ServiceFor(TerrainType.Open);

            await sut.GrantMaterials(_player.Id, new Dictionary<int, int>
            {
                [await MaterialId("scrap")] = 3,
                [await MaterialId("timber_rough")] = 4,
            }, Ct);

            var inventory = await sut.GetInventory(_player.Id, Ct);

            Assert.That(inventory.Categories.Select(c => c.Category),
                Is.EquivalentTo(new[] { MaterialCategory.Urban, MaterialCategory.Woodland }));
        }

        // ── Level gating, end to end ────────────────────────────────────

        [Test]
        public async Task ALockedSkill_NeverYieldsItsMaterialsThroughTheService()
        {
            // Mining is not unlocked at Adventurer level 1, so no rocky material may appear
            // however many rocky cells are walked.
            var sut = ServiceFor(TerrainType.Rocky);

            var cells = Enumerable.Range(0, 60).Select(i => new GridCell(200 + i, 300 + i)).ToList();
            var gains = await sut.AwardCellDrops(_player.Id, cells, Ct);

            var rockyKeys = MaterialSeedData.Materials
                .Where(m => m.Category == MaterialCategory.Rocky)
                .Select(m => m.Key)
                .ToHashSet();

            Assert.That(gains.Any(g => rockyKeys.Contains(g.Key)), Is.False,
                "Mining is locked, so rocky materials must not drop at all");
        }
    }
}
