using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Crafting;
using GeoSlayer.Domain.Services.Fog;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Domain.Services.Skills;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Tests.Services.Skills
{
    /// <summary>
    /// Stage 09 — Cooking, the first <b>production</b> skill.
    ///
    /// <para>The stage note asks specifically whether a skill with <b>no terrain mapping</b>
    /// still levels fine. That is a different code path from gathering, exercised here for
    /// the first time, and it is what most of these tests are about.</para>
    /// </summary>
    [TestFixture]
    public class CookingIntegrationTests : DatabaseIntegrationTestBase
    {
        private ProgressionService _progression = null!;
        private MaterialService _materials = null!;
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
            await TestDatabaseSeedHelper.SeedCraftingDefinitions(DbContext);

            _progression = TestDatabaseSeedHelper.CreateProgressionService(DbContext);
            _materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext);
            _crafting = TestDatabaseSeedHelper.CreateCraftingService(DbContext, _progression, _materials);

            await _progression.EnsureStartingUnlocks(_player.Id, Ct);
        }

        private async Task Unlock(SkillType skill, int level = 1)
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

        private async Task GiveMaterial(string key, int quantity)
        {
            var material = await DbContext.Materials.FirstAsync(m => m.Key == key);

            var row = await DbContext.PlayerMaterials
                .FirstOrDefaultAsync(pm => pm.PlayerId == _player.Id && pm.MaterialId == material.Id);

            if (row is null)
            {
                row = new PlayerMaterial { PlayerId = _player.Id, MaterialId = material.Id };
                DbContext.PlayerMaterials.Add(row);
            }

            row.Quantity += quantity;
            await DbContext.SaveChangesAsync();
        }

        private async Task<long> Held(string key)
        {
            var material = await DbContext.Materials.FirstAsync(m => m.Key == key);

            return await DbContext.PlayerMaterials
                .Where(pm => pm.PlayerId == _player.Id && pm.MaterialId == material.Id)
                .Select(pm => pm.Quantity)
                .FirstOrDefaultAsync();
        }

        // ── Criterion 2: unlock timing ──────────────────────────────────

        [Test]
        public async Task Cooking_IsNotUnlockedBeforeAdventurerEight()
        {
            var has = await DbContext.PlayerSkills
                .AnyAsync(s => s.PlayerId == _player.Id && s.SkillType == SkillType.Cooking);

            Assert.That(has, Is.False);
        }

        [Test]
        public async Task Cooking_UnlocksAtAdventurerEight()
        {
            var target = XpCurve.XpForLevel(8);
            var result = await _progression.GrantXp(_player.Id, null, target * 4, XpSource.Walk, Ct);

            var has = await DbContext.PlayerSkills
                .AnyAsync(s => s.PlayerId == _player.Id && s.SkillType == SkillType.Cooking);

            Assert.Multiple(() =>
            {
                Assert.That(has, Is.True);
                Assert.That(result.Unlocks.Any(u => u.Payload == nameof(SkillType.Cooking)), Is.True);
            });
        }

        [Test]
        public async Task Cooking_IsSeededAsAProductionSkill()
        {
            var definition = await DbContext.SkillDefinitions
                .FirstOrDefaultAsync(d => d.SkillType == SkillType.Cooking);

            Assert.Multiple(() =>
            {
                Assert.That(definition, Is.Not.Null);
                Assert.That(definition!.Category, Is.EqualTo(SkillCategory.Production));
                Assert.That(definition.UnlockLevel, Is.EqualTo(8));
            });
        }

        // ── The stage note: no terrain mapping, still levels ────────────

        [Test]
        public async Task Cooking_HasNoTerrainMapping()
        {
            var mappings = await DbContext.SkillTerrainMappings
                .Where(m => m.SkillType == SkillType.Cooking)
                .ToListAsync();

            Assert.That(mappings, Is.Empty,
                "a production skill must not train by walking");
        }

        [Test]
        public async Task WalkingDoesNotTrainCooking()
        {
            await Unlock(SkillType.Cooking);

            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Woodland);
            var training = TestDatabaseSeedHelper.CreateSkillTrainingService(DbContext, _progression, materials);

            var results = await training.TrainFromCells(
                _player.Id,
                Enumerable.Range(0, 10).Select(i => new GridCell(10 + i, 10)).ToList(),
                Ct);

            Assert.That(results.Any(r => r.SkillType == SkillType.Cooking), Is.False,
                "Cooking is trained at the fire, not on the road");
        }

        [Test]
        public async Task CraftingTrainsCooking_DespiteNoTerrain()
        {
            // The stage note's actual question: does a skill with no terrain mapping still
            // level? It must, through the craft queue.
            await Unlock(SkillType.Cooking);
            await GiveMaterial("wild_grass", 20);
            await GiveMaterial("minnow", 20);

            var before = await DbContext.PlayerSkills
                .Where(s => s.PlayerId == _player.Id && s.SkillType == SkillType.Cooking)
                .Select(s => s.Xp)
                .FirstAsync();

            var craft = await _crafting.QueueCraft(_player.Id, "dried_rations", Ct);

            var row = await DbContext.PlayerCrafts.FirstAsync(c => c.Id == craft.Id);
            row.CompletesUtc = DateTime.UtcNow.AddSeconds(-1);
            await DbContext.SaveChangesAsync();

            var collected = await _crafting.CollectCompletedCrafts(_player.Id, Ct);

            var after = await DbContext.PlayerSkills
                .Where(s => s.PlayerId == _player.Id && s.SkillType == SkillType.Cooking)
                .Select(s => s.Xp)
                .FirstAsync();

            Assert.Multiple(() =>
            {
                Assert.That(after, Is.GreaterThan(before), "crafting must level a production skill");
                Assert.That(collected.SkillXpEarned, Is.GreaterThan(0));
                Assert.That(collected.AdventurerXpEarned, Is.GreaterThan(0),
                    "and pay the Adventurer cut like any other XP");
            });
        }

        [Test]
        public async Task CookedFood_NeverDropsFromWalking()
        {
            // A pie must not be found in a hedge. This also protects the craft queue: if food
            // dropped, the skill that exists to produce it would be bypassed.
            await Unlock(SkillType.Cooking, 90);

            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Woodland);

            var gains = await materials.AwardCellDrops(
                _player.Id,
                Enumerable.Range(0, 60).Select(i => new GridCell(100 + i, 100)).ToList(),
                Ct);

            var cookedKeys = SkillSeedData.CookingMaterials.Select(m => m.Key).ToHashSet();

            Assert.That(gains.Any(g => cookedKeys.Contains(g.Key)), Is.False,
                "cooked food is made, never found");
        }

        // ── The chain: production consumes gathering ────────────────────

        [Test]
        public async Task CookingConsumesGatheredMaterials()
        {
            await Unlock(SkillType.Cooking);
            await GiveMaterial("wild_grass", 20);
            await GiveMaterial("minnow", 20);

            var grassBefore = await Held("wild_grass");
            var minnowBefore = await Held("minnow");

            await _crafting.QueueCraft(_player.Id, "dried_rations", Ct);

            Assert.Multiple(async () =>
            {
                Assert.That(await Held("wild_grass"), Is.EqualTo(grassBefore - 3));
                Assert.That(await Held("minnow"), Is.EqualTo(minnowBefore - 2));
            });
        }

        [Test]
        public async Task HigherTierRecipes_ConsumeHigherTierInputs()
        {
            // This is what chains Cooking to its gathering counterparts rather than leaving
            // them as parallel bars (§4.1a, production inverted).
            var recipes = await DbContext.Recipes
                .Include(r => r.Inputs).ThenInclude(i => i.Material)
                .Where(r => r.SkillType == SkillType.Cooking)
                .OrderBy(r => r.LevelRequired)
                .ToListAsync();

            Assert.That(recipes, Has.Count.EqualTo(7));

            var previousInputTier = 0;

            foreach (var recipe in recipes)
            {
                var maxInputTier = recipe.Inputs.Max(i => i.Material.Tier);

                Assert.That(maxInputTier, Is.GreaterThanOrEqualTo(previousInputTier),
                    $"{recipe.Name} consumes lower-tier inputs than the recipe below it");

                previousInputTier = maxInputTier;
            }
        }

        [Test]
        public async Task ACookingRecipeAboveLevel_IsLevelLocked()
        {
            await Unlock(SkillType.Cooking);

            var list = await _crafting.GetRecipes(_player.Id, Ct);
            var pie = list.Recipes.First(r => r.Key == "hearty_pie");

            Assert.Multiple(() =>
            {
                Assert.That(pie.CanCraft, Is.False);
                Assert.That(pie.LockText, Does.Contain("level 20"));
            });
        }

        [Test]
        public async Task CookedFood_AppearsInInventoryWithCaps()
        {
            await Unlock(SkillType.Cooking);
            await GiveMaterial("dried_rations", 5);

            var inventory = await _materials.GetInventory(_player.Id, Ct);
            var item = inventory.Categories.SelectMany(c => c.Items).First(i => i.Key == "dried_rations");

            Assert.Multiple(() =>
            {
                Assert.That(item.Quantity, Is.EqualTo(5));
                Assert.That(item.StackCap, Is.GreaterThan(0));
            });
        }
    }
}
