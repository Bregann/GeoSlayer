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
    /// Stage 10's stage note: <b>the accessibility stress test</b>.
    ///
    /// <para>Mining terrain is genuinely rare — most players have no quarry, mineshaft or
    /// cave anywhere near them. This is the skill that proves §4.1a's tier-gates-on-level
    /// rule actually holds. The note is explicit: if a player with zero rocky terrain cannot
    /// reach tier 7 Meteoric Ore purely by levelling, the rule was broken somewhere and
    /// <i>every other skill is suspect too</i>.</para>
    ///
    /// <para>So these tests deliberately go further than the generic ladder suite: they walk
    /// a simulated suburban player through the whole ladder on terrain that never matches.</para>
    /// </summary>
    [TestFixture]
    public class MiningAccessibilityTests : DatabaseIntegrationTestBase
    {
        private ProgressionService _progression = null!;
        private Player _player = null!;

        private static CancellationToken Ct => CancellationToken.None;

        /// <summary>Terrain a suburban player actually has. Deliberately never Rocky.</summary>
        private static readonly TerrainType[] SuburbanTerrain =
        [
            TerrainType.Urban,
            TerrainType.Open,
            TerrainType.Woodland,
            TerrainType.Farmland,
        ];

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

        private async Task SetMining(int level)
        {
            var row = await DbContext.PlayerSkills
                .FirstOrDefaultAsync(s => s.PlayerId == _player.Id && s.SkillType == SkillType.Mining);

            if (row is null)
            {
                row = new PlayerSkill
                {
                    PlayerId = _player.Id,
                    SkillType = SkillType.Mining,
                    UnlockedAtUtc = DateTime.UtcNow,
                };
                DbContext.PlayerSkills.Add(row);
            }

            row.Level = level;
            row.Xp = XpCurve.XpForLevel(level);
            await DbContext.SaveChangesAsync();
        }

        // ── The headline: tier 7 without a quarry ───────────────────────

        [Test]
        public async Task ASuburbanPlayerAtMiningNinety_ObtainsMeteoricOre()
        {
            // The stage note's explicit demand. If this fails, §4.1a is broken everywhere.
            await SetMining(90);

            var found = false;

            foreach (var terrain in SuburbanTerrain)
            {
                var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, terrain);

                var gains = await materials.AwardCellDrops(
                    _player.Id,
                    Enumerable.Range(0, 80)
                        .Select(i => new GridCell((int)terrain * 1000 + i, 400))
                        .ToList(),
                    Ct);

                if (gains.Any(g => g.Key == "meteoric_ore")) { found = true; break; }
            }

            Assert.That(found, Is.True,
                "a player with no rocky terrain must still reach tier 7 by levelling alone");
        }

        [Test]
        public async Task EveryMiningTier_IsReachableWithoutRockyTerrain()
        {
            // Not just the top tier — the whole ladder must be walkable from a suburb.
            foreach (var material in SkillSeedData.MiningMaterials)
            {
                await SetMining(material.LevelRequired);

                var found = false;

                foreach (var terrain in SuburbanTerrain)
                {
                    var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, terrain);

                    var gains = await materials.AwardCellDrops(
                        _player.Id,
                        Enumerable.Range(0, 60)
                            .Select(i => new GridCell(material.Tier * 5000 + (int)terrain * 200 + i, 500))
                            .ToList(),
                        Ct);

                    if (gains.Any(g => g.Key == material.Key)) { found = true; break; }
                }

                Assert.That(found, Is.True,
                    $"{material.Name} (tier {material.Tier}, level {material.LevelRequired}) " +
                    "is unreachable without rocky terrain");
            }
        }

        [Test]
        public async Task UrbanGround_TrainsMiningAtBaseRate()
        {
            await SetMining(1);

            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Urban);
            var training = TestDatabaseSeedHelper.CreateSkillTrainingService(DbContext, _progression, materials);

            var results = await training.TrainFromCells(_player.Id, [new GridCell(10, 10)], Ct);
            var mining = results.FirstOrDefault(r => r.SkillType == SkillType.Mining);

            Assert.Multiple(() =>
            {
                Assert.That(mining, Is.Not.Null, "a city street must still train Mining");
                Assert.That(mining!.SkillXpEarned, Is.EqualTo(1), "at base rate");
            });
        }

        // ── The compensating difference: slower, not blocked ────────────

        [Test]
        public async Task RockyGround_IsMeaningfullyFasterWithoutBeingRequired()
        {
            // §4.1a wants the difference "meaningful, not disqualifying". Quantifying it here
            // makes the trade-off visible rather than assumed: if this ratio ever grows large
            // enough to feel disqualifying, this test is where it shows up.
            await SetMining(20);

            var urbanGains = await TestDatabaseSeedHelper
                .CreateMaterialService(DbContext, TerrainType.Urban)
                .AwardCellDrops(
                    _player.Id,
                    Enumerable.Range(0, 100).Select(i => new GridCell(20_000 + i, 600)).ToList(),
                    Ct);

            var rockyGains = await TestDatabaseSeedHelper
                .CreateMaterialService(DbContext, TerrainType.Rocky)
                .AwardCellDrops(
                    _player.Id,
                    Enumerable.Range(0, 100).Select(i => new GridCell(30_000 + i, 600)).ToList(),
                    Ct);

            var miningKeys = SkillSeedData.MiningMaterials.Select(m => m.Key).ToHashSet();

            var urban = urbanGains.Where(g => miningKeys.Contains(g.Key)).Sum(g => g.Quantity);
            var rocky = rockyGains.Where(g => miningKeys.Contains(g.Key)).Sum(g => g.Quantity);

            Assert.Multiple(() =>
            {
                Assert.That(urban, Is.GreaterThan(0), "urban must yield ore, not zero");
                Assert.That(rocky, Is.GreaterThan(urban), "but rocky should be clearly better");

                // A ratio this side of ~4x keeps the gap meaningful without being punishing.
                Assert.That((double)rocky / urban, Is.LessThan(4.0),
                    $"rocky yields {(double)rocky / urban:F1}x urban — that is starting to look disqualifying");
            });
        }

        [Test]
        public async Task MiningUnlocksAtAdventurerTwelve()
        {
            var has = await DbContext.PlayerSkills
                .AnyAsync(s => s.PlayerId == _player.Id && s.SkillType == SkillType.Mining);

            Assert.That(has, Is.False, "not before level 12");

            var target = XpCurve.XpForLevel(12);
            var result = await _progression.GrantXp(_player.Id, null, target * 4, XpSource.Walk, Ct);

            Assert.Multiple(async () =>
            {
                Assert.That(
                    await DbContext.PlayerSkills.AnyAsync(
                        s => s.PlayerId == _player.Id && s.SkillType == SkillType.Mining),
                    Is.True);

                Assert.That(result.Unlocks.Any(u => u.Payload == nameof(SkillType.Mining)), Is.True);
            });
        }

        // ── Tool gating (§4.3), deferred from Stage 06 to here ─────────

        [Test]
        public async Task WithNoToolEquipped_EveryTierIsStillReachable()
        {
            // The reason the gate was deferred: applying a cap to a player with no tool would
            // silently lock every skill behind gear the tutorial never teaches. No tool must
            // mean no cap, not tier 1.
            await SetMining(90);

            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Rocky);

            var gains = await materials.AwardCellDrops(
                _player.Id,
                Enumerable.Range(0, 80).Select(i => new GridCell(40_000 + i, 700)).ToList(),
                Ct);

            Assert.That(gains.Any(g => g.Key == "meteoric_ore"), Is.True,
                "no tool must not mean a tier cap");
        }

        [Test]
        public async Task AnEquippedTool_CapsTheTierItPermits()
        {
            await TestDatabaseSeedHelper.SeedCraftingDefinitions(DbContext);
            await SetMining(90);

            // The Foraging Knife permits up to tier 3.
            var knife = await DbContext.Items.FirstAsync(i => i.Key == "foraging_knife");

            DbContext.PlayerItems.Add(new PlayerItem
            {
                PlayerId = _player.Id,
                ItemId = knife.Id,
                Quantity = 1,
                IsEquipped = true,
                AcquiredUtc = DateTime.UtcNow,
            });
            await DbContext.SaveChangesAsync();

            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Rocky);

            var gains = await materials.AwardCellDrops(
                _player.Id,
                Enumerable.Range(0, 80).Select(i => new GridCell(50_000 + i, 800)).ToList(),
                Ct);

            var aboveCap = SkillSeedData.MiningMaterials
                .Where(m => m.Tier > 3)
                .Select(m => m.Key)
                .ToHashSet();

            Assert.Multiple(() =>
            {
                Assert.That(gains.Any(g => aboveCap.Contains(g.Key)), Is.False,
                    "a tier-3 tool must not yield tier 4+");
                Assert.That(gains, Is.Not.Empty, "but it must still yield what it permits");
            });
        }

        [Test]
        public async Task ABetterTool_RaisesTheCap()
        {
            await TestDatabaseSeedHelper.SeedCraftingDefinitions(DbContext);
            await SetMining(90);

            // The Harvest Sickle permits up to tier 5 — this is what gives crafting a purpose
            // beyond stat creep (§4.3).
            var sickle = await DbContext.Items.FirstAsync(i => i.Key == "harvest_sickle");

            DbContext.PlayerItems.Add(new PlayerItem
            {
                PlayerId = _player.Id,
                ItemId = sickle.Id,
                Quantity = 1,
                IsEquipped = true,
                AcquiredUtc = DateTime.UtcNow,
            });
            await DbContext.SaveChangesAsync();

            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Rocky);

            var gains = await materials.AwardCellDrops(
                _player.Id,
                Enumerable.Range(0, 120).Select(i => new GridCell(60_000 + i, 900)).ToList(),
                Ct);

            var tierFiveOrBelow = SkillSeedData.MiningMaterials
                .Where(m => m.Tier is >= 4 and <= 5)
                .Select(m => m.Key)
                .ToHashSet();

            var aboveCap = SkillSeedData.MiningMaterials
                .Where(m => m.Tier > 5)
                .Select(m => m.Key)
                .ToHashSet();

            Assert.Multiple(() =>
            {
                Assert.That(gains.Any(g => tierFiveOrBelow.Contains(g.Key)), Is.True,
                    "a tier-5 tool should reach tier 4-5");
                Assert.That(gains.Any(g => aboveCap.Contains(g.Key)), Is.False,
                    "but not beyond");
            });
        }

        [Test]
        public void MiningLadderMatchesTheDesignWorkedExample()
        {
            // §4.1a uses Mining as its worked example, naming these exact materials and
            // levels. A drift here means the document and the game disagree.
            var expected = new[]
            {
                ("Rough Stone", 1), ("Copper Ore", 10), ("Iron Ore", 20), ("Silver Ore", 35),
                ("Gold Ore", 50), ("Gemstone", 70), ("Meteoric Ore", 90),
            };

            var ladder = SkillSeedData.MiningMaterials.OrderBy(m => m.Tier).ToList();

            for (var i = 0; i < expected.Length; i++)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(ladder[i].Name, Is.EqualTo(expected[i].Item1));
                    Assert.That(ladder[i].LevelRequired, Is.EqualTo(expected[i].Item2));
                });
            }
        }
    }
}
