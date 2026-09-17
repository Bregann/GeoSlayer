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
    /// Stage 11 — Smithing, and the loop it closes.
    ///
    /// <para>The stage note asks for two things specifically: that Smithing consumes Mining
    /// output <b>tier for tier</b>, and that tool tiers <b>actually reduce gather time</b>.
    /// The second is the one that makes Mining worth levelling, so it is tested against
    /// observed yield rather than a stored number.</para>
    /// </summary>
    [TestFixture]
    public class SmithingIntegrationTests : DatabaseIntegrationTestBase
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
                    PlayerId = _player.Id, SkillType = skill, UnlockedAtUtc = DateTime.UtcNow,
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

        private async Task EquipTool(string key)
        {
            var item = await DbContext.Items.FirstAsync(i => i.Key == key);

            DbContext.PlayerItems.Add(new PlayerItem
            {
                PlayerId = _player.Id,
                ItemId = item.Id,
                Quantity = 1,
                IsEquipped = true,
                AcquiredUtc = DateTime.UtcNow,
            });

            await DbContext.SaveChangesAsync();
        }

        /// <summary>Total Mining materials yielded by a fixed synthetic walk.</summary>
        private async Task<int> MinedOverFixedWalk(int seedOffset)
        {
            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Rocky);

            var gains = await materials.AwardCellDrops(
                _player.Id,
                Enumerable.Range(0, 60).Select(i => new GridCell(seedOffset + i, 400)).ToList(),
                Ct);

            var miningKeys = SkillSeedData.MiningMaterials.Select(m => m.Key).ToHashSet();

            return gains.Where(g => miningKeys.Contains(g.Key)).Sum(g => g.Quantity);
        }

        // ── Unlock ──────────────────────────────────────────────────────

        [Test]
        public async Task Smithing_UnlocksAtAdventurerSixteen()
        {
            var has = await DbContext.PlayerSkills
                .AnyAsync(s => s.PlayerId == _player.Id && s.SkillType == SkillType.Smithing);

            Assert.That(has, Is.False, "not before 16");

            var target = XpCurve.XpForLevel(16);
            await _progression.GrantXp(_player.Id, null, target * 4, XpSource.Walk, Ct);

            Assert.That(
                await DbContext.PlayerSkills.AnyAsync(
                    s => s.PlayerId == _player.Id && s.SkillType == SkillType.Smithing),
                Is.True);
        }

        [Test]
        public async Task Smithing_IsAProductionSkill()
        {
            var definition = await DbContext.SkillDefinitions
                .FirstAsync(d => d.SkillType == SkillType.Smithing);

            var mappings = await DbContext.SkillTerrainMappings
                .Where(m => m.SkillType == SkillType.Smithing)
                .ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(definition.Category, Is.EqualTo(SkillCategory.Production));
                Assert.That(mappings, Is.Empty, "production skills train by crafting");
            });
        }

        // ── The chain: consumes Mining output tier for tier ─────────────

        [Test]
        public async Task EverySmithingRecipe_ConsumesTheMatchingMiningTier()
        {
            // The stage note's first demand. Tier-for-tier coupling is what makes these two
            // skills a chain rather than parallel bars.
            var recipes = await DbContext.Recipes
                .Include(r => r.Inputs).ThenInclude(i => i.Material)
                .Where(r => r.SkillType == SkillType.Smithing)
                .OrderBy(r => r.LevelRequired)
                .ToListAsync();

            Assert.That(recipes, Has.Count.EqualTo(7));

            for (var i = 0; i < recipes.Count; i++)
            {
                var expectedTier = i + 1;
                var inputs = recipes[i].Inputs.ToList();

                Assert.That(inputs, Has.Count.GreaterThan(0), $"{recipes[i].Name} has no inputs");

                foreach (var input in inputs)
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(input.Material.SkillType, Is.EqualTo(SkillType.Mining),
                            $"{recipes[i].Name} should consume Mining output");
                        Assert.That(input.Material.Tier, Is.EqualTo(expectedTier),
                            $"{recipes[i].Name} (tier {expectedTier}) consumes tier {input.Material.Tier}");
                    });
                }
            }
        }

        [Test]
        public async Task SmithingConsumesOreAndProducesATool()
        {
            await Unlock(SkillType.Smithing);
            await GiveMaterial("stone_rough", 20);

            var craft = await _crafting.QueueCraft(_player.Id, "smith_stone_tools", Ct);

            var row = await DbContext.PlayerCrafts.FirstAsync(c => c.Id == craft.Id);
            row.CompletesUtc = DateTime.UtcNow.AddSeconds(-1);
            await DbContext.SaveChangesAsync();

            var collected = await _crafting.CollectCompletedCrafts(_player.Id, Ct);

            Assert.That(collected.Items.Any(i => i.Key == "stone_tools"), Is.True);
        }

        // ── The stage note's second demand: tools reduce gather time ────

        [Test]
        public async Task ABetterTool_MeasurablyIncreasesYieldPerWalk()
        {
            // "Verify tool tiers actually reduce BaseGatherSeconds." A walk cannot block on a
            // timer, so faster gathering shows up as more units per cell — the same way
            // §4.1a expresses gather time on foot. Asserted against observed yield rather
            // than the stored modifier, because a stored number that nothing reads is exactly
            // the bug §4.3 warns about.
            await Unlock(SkillType.Mining, 20);

            // Iron Pickaxe: reaches tier 3, and 20% faster.
            await EquipTool("iron_pickaxe");
            var withIron = await MinedOverFixedWalk(70_000);

            // Swap to Stone Tools: also reaches the player's tier here, but no speed bonus.
            var iron = await DbContext.PlayerItems
                .Include(pi => pi.Item)
                .FirstAsync(pi => pi.PlayerId == _player.Id && pi.Item.Key == "iron_pickaxe");

            iron.IsEquipped = false;
            await DbContext.SaveChangesAsync();

            await EquipTool("copper_pickaxe");
            var withCopper = await MinedOverFixedWalk(70_000);

            Assert.Multiple(() =>
            {
                Assert.That(withCopper, Is.GreaterThan(0));
                Assert.That(withIron, Is.GreaterThan(withCopper),
                    "a 20% tool should out-yield a 10% tool on the same walk");
            });
        }

        [Test]
        public async Task AToolsSpeedBonus_IsVisibleThroughTheModifierTotal()
        {
            await EquipTool("iron_pickaxe");

            var speed = await _crafting.GetModifierTotal(
                _player.Id, ItemModifier.GatherSpeedPercent, Ct);

            // The bonus rides as a *secondary* modifier on the same item as the tier gate,
            // because two items would compete for the single Tool slot and make the speed
            // unequippable. GetModifierTotal must see both.
            Assert.That(speed, Is.EqualTo(0.2).Within(0.0001));
        }

        [Test]
        public async Task AToolStillGatesTier_WhileAlsoGrantingSpeed()
        {
            // One item, two jobs. The gate must not be lost by adding the speed bonus.
            await Unlock(SkillType.Mining, 90);
            await EquipTool("copper_pickaxe");   // tier 2

            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Rocky);

            var gains = await materials.AwardCellDrops(
                _player.Id,
                Enumerable.Range(0, 80).Select(i => new GridCell(80_000 + i, 500)).ToList(),
                Ct);

            var aboveCap = SkillSeedData.MiningMaterials
                .Where(m => m.Tier > 2)
                .Select(m => m.Key)
                .ToHashSet();

            Assert.Multiple(() =>
            {
                Assert.That(gains, Is.Not.Empty, "it should still yield its own tiers");
                Assert.That(gains.Any(g => aboveCap.Contains(g.Key)), Is.False,
                    "a tier-2 tool must still cap at tier 2");
            });
        }

        [Test]
        public void ToolSpeed_RisesWithTier()
        {
            // Otherwise a higher tool is only an access upgrade, and there is no reason to
            // re-craft once you can already reach your tier.
            //
            // Grouped by tier rather than compared pairwise: two tools can legitimately share
            // a tier (a Foraging Knife and an Iron Pickaxe are both tier 3), and they should
            // then share a speed. What must hold is that a *higher* tier is strictly faster.
            var byTier = RecipeSeedData.Items
                .Where(i => i.Modifier == ItemModifier.ToolTier)
                .GroupBy(i => (int)i.ModifierValue)
                .OrderBy(g => g.Key)
                .ToList();

            Assert.That(byTier, Has.Count.GreaterThanOrEqualTo(7));

            foreach (var tier in byTier)
            {
                var speeds = tier.Select(i => i.SecondaryModifierValue).Distinct().ToList();

                Assert.That(speeds, Has.Count.EqualTo(1),
                    $"tier {tier.Key} tools disagree on speed: " +
                    string.Join(", ", tier.Select(i => $"{i.Name}={i.SecondaryModifierValue}")));
            }

            for (var i = 1; i < byTier.Count; i++)
            {
                Assert.That(byTier[i].First().SecondaryModifierValue,
                    Is.GreaterThan(byTier[i - 1].First().SecondaryModifierValue),
                    $"tier {byTier[i].Key} is no faster than tier {byTier[i - 1].Key}");
            }
        }

        [Test]
        public async Task AToolsDisplayText_MentionsBothItsEffects()
        {
            // Showing only the tier gate would undersell every tool — a player comparing an
            // Iron Pickaxe to a Foraging Knife would see no difference at all.
            await EquipTool("iron_pickaxe");

            var items = await _crafting.GetItems(_player.Id, Ct);
            var pickaxe = items.First(i => i.Key == "iron_pickaxe");

            Assert.Multiple(() =>
            {
                Assert.That(pickaxe.ModifierText, Does.Contain("tier 3"));
                Assert.That(pickaxe.ModifierText, Does.Contain("faster"),
                    "the speed bonus must be visible, not just stored");
            });
        }

        [Test]
        public void EveryToolCarriesBothATierGateAndASpeedBonus()
        {
            var tools = RecipeSeedData.Items
                .Where(i => i.Kind == ItemKind.Tool && i.Modifier == ItemModifier.ToolTier)
                .ToList();

            foreach (var tool in tools)
            {
                Assert.That(tool.SecondaryModifier, Is.EqualTo(ItemModifier.GatherSpeedPercent),
                    $"{tool.Name} has no speed bonus — it would be an access-only upgrade");
            }
        }
    }
}
