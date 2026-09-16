using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Interfaces.Helpers;
using GeoSlayer.Domain.Services.Crafting;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Domain.Services.Skills;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Helpers
{
    public class DatabaseSeedHelper
    {
        public static async Task SeedDatabase(AppDbContext context, IEnvironmentalSettingHelper settingsHelper, IServiceProvider serviceProvider)
        {
            await SeedSetting(context, EnvironmentalSettingEnum.HangfireUsername, "admin");
            await SeedSetting(context, EnvironmentalSettingEnum.HangfirePassword, "password");

            await SeedProgressionSettings(context);
            await SeedUnlockLadder(context);
            await SeedUpgrades(context);
            await SeedMaterials(context);
            await SeedSkillDefinitions(context);
            await SeedSkillTerrainMappings(context);

            await context.SaveChangesAsync();

            // Drop entries reference material ids, so they need the materials persisted.
            await SeedDropTables(context);
            await SeedItems(context);
            await context.SaveChangesAsync();

            // Recipes reference both material and item ids.
            await SeedRecipes(context);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Progression constants (§3.0b, §3.3). Seeded rather than compiled in so the
        /// whole progression spine can be retuned without a deploy.
        /// </summary>
        private static async Task SeedProgressionSettings(AppDbContext context)
        {
            await SeedSetting(context, EnvironmentalSettingEnum.GlobalXpRatio,
                Invariant(ProgressionDefaults.GlobalXpRatio));

            await SeedSetting(context, EnvironmentalSettingEnum.IdleXpRatioMultiplier,
                Invariant(ProgressionDefaults.IdleXpRatioMultiplier));

            await SeedSetting(context, EnvironmentalSettingEnum.RespecBaseCost,
                ProgressionDefaults.RespecBaseCost.ToString());

            await SeedSetting(context, EnvironmentalSettingEnum.MilestoneXpNewCell,
                ProgressionDefaults.MilestoneXp[MilestoneType.NewCell].ToString());

            await SeedSetting(context, EnvironmentalSettingEnum.MilestoneXpFirstPoiVisit,
                ProgressionDefaults.MilestoneXp[MilestoneType.FirstPoiVisit].ToString());

            await SeedSetting(context, EnvironmentalSettingEnum.MilestoneXpNewRegion,
                ProgressionDefaults.MilestoneXp[MilestoneType.NewRegion].ToString());

            await SeedSetting(context, EnvironmentalSettingEnum.MilestoneXpClaimTerritory,
                ProgressionDefaults.MilestoneXp[MilestoneType.ClaimTerritory].ToString());

            await SeedSetting(context, EnvironmentalSettingEnum.MilestoneXpCraftComplete,
                ProgressionDefaults.MilestoneXp[MilestoneType.CraftComplete].ToString());

            await SeedSetting(context, EnvironmentalSettingEnum.MilestoneXpSkillUnlock,
                ProgressionDefaults.MilestoneXp[MilestoneType.SkillUnlock].ToString());
        }

        /// <summary>
        /// The unlock ladder (§3.1). Inserts missing rungs and leaves existing rows alone,
        /// so a retuned live ladder is not stamped back to defaults on the next boot.
        /// </summary>
        private static async Task SeedUnlockLadder(AppDbContext context)
        {
            var existing = await context.UnlockDefinitions
                .Select(u => new { u.AdventurerLevel, u.Payload })
                .ToListAsync();

            var have = existing
                .Select(e => (e.AdventurerLevel, e.Payload))
                .ToHashSet();

            foreach (var rung in ProgressionSeedData.Ladder)
            {
                if (have.Contains((rung.AdventurerLevel, rung.Payload))) continue;

                context.UnlockDefinitions.Add(new UnlockDefinition
                {
                    AdventurerLevel = rung.AdventurerLevel,
                    UnlockType = rung.UnlockType,
                    Payload = rung.Payload,
                    DisplayName = rung.DisplayName,
                });
            }
        }

        /// <summary>The four Stage 02 Bonus Point upgrades (§3.0a).</summary>
        private static async Task SeedUpgrades(AppDbContext context)
        {
            var have = (await context.UpgradeDefinitions.Select(u => u.Key).ToListAsync())
                .ToHashSet();

            foreach (var upgrade in ProgressionSeedData.Upgrades)
            {
                if (have.Contains(upgrade.Key)) continue;

                context.UpgradeDefinitions.Add(new UpgradeDefinition
                {
                    Key = upgrade.Key,
                    Name = upgrade.Name,
                    Category = upgrade.Category,
                    MaxRank = upgrade.MaxRank,
                    CostCurve = upgrade.CostCurve,
                    EffectPerRank = upgrade.EffectPerRank,
                    MinAdventurerLevel = upgrade.MinAdventurerLevel,
                    Description = upgrade.Description,
                });
            }
        }

        /// <summary>Material pools (§4.1, §4.1a, §7.4). Inserts missing keys only.</summary>
        private static async Task SeedMaterials(AppDbContext context)
        {
            var have = (await context.Materials.Select(m => m.Key).ToListAsync()).ToHashSet();

            foreach (var material in MaterialSeedData.Materials.Concat(SkillSeedData.ForagingMaterials))
            {
                if (!have.Add(material.Key)) continue;

                context.Materials.Add(new Material
                {
                    Key = material.Key,
                    Name = material.Name,
                    Tier = material.Tier,
                    Category = material.Category,
                    SkillType = material.SkillType,
                    StackCap = material.StackCap,
                    IsUnique = material.IsUnique,
                    LevelRequired = material.LevelRequired,
                    BaseGatherSeconds = material.BaseGatherSeconds,
                    XpPerUnit = material.XpPerUnit,
                    DustPerOverflow = material.DustPerOverflow,
                });
            }
        }

        /// <summary>
        /// Terrain drop tables (task 4), derived from the seeded pools rather than listed
        /// separately so the two cannot drift.
        /// </summary>
        private static async Task SeedDropTables(AppDbContext context)
        {
            var materialIds = await context.Materials.ToDictionaryAsync(m => m.Key, m => m.Id);

            var have = (await context.DropTableEntries
                    .Select(e => new { e.Terrain, e.MaterialId })
                    .ToListAsync())
                .Select(e => (e.Terrain, e.MaterialId))
                .ToHashSet();

            foreach (var (terrain, key, weight, min, max) in
                     MaterialSeedData.DropEntries().Concat(SkillSeedData.DropEntries()))
            {
                if (!materialIds.TryGetValue(key, out var materialId)) continue;
                if (have.Contains((terrain, materialId))) continue;

                have.Add((terrain, materialId));

                context.DropTableEntries.Add(new DropTableEntry
                {
                    Terrain = terrain,
                    MaterialId = materialId,
                    Weight = weight,
                    MinQuantity = min,
                    MaxQuantity = max,
                });
            }
        }

        /// <summary>Skill metadata for the skills screen (Stage 04 task 1).</summary>
        private static async Task SeedSkillDefinitions(AppDbContext context)
        {
            var have = (await context.SkillDefinitions.Select(d => d.SkillType).ToListAsync())
                .ToHashSet();

            foreach (var definition in SkillSeedData.Definitions)
            {
                if (have.Contains(definition.SkillType)) continue;

                context.SkillDefinitions.Add(new SkillDefinition
                {
                    SkillType = definition.SkillType,
                    Name = definition.Name,
                    Description = definition.Description,
                    Icon = definition.Icon,
                    UnlockLevel = definition.UnlockLevel,
                    Category = definition.Category,
                });
            }
        }

        /// <summary>
        /// Which terrain trains which skill (Stage 04 task 1). Terrain multiplies, never
        /// gates — every gathering skill needs an Open row for that to hold.
        /// </summary>
        private static async Task SeedSkillTerrainMappings(AppDbContext context)
        {
            var existing = await context.SkillTerrainMappings
                .Select(m => new { m.SkillType, m.Terrain })
                .ToListAsync();

            var have = existing.Select(m => (m.SkillType, m.Terrain)).ToHashSet();

            foreach (var mapping in SkillSeedData.TerrainMappings)
            {
                if (have.Contains((mapping.SkillType, mapping.Terrain))) continue;

                context.SkillTerrainMappings.Add(new SkillTerrainMapping
                {
                    SkillType = mapping.SkillType,
                    Terrain = mapping.Terrain,
                    XpPerCell = mapping.XpPerCell,
                    YieldMultiplier = mapping.YieldMultiplier,
                });
            }
        }

        /// <summary>Gear, tools and buildings from the embedded JSON (§4.3).</summary>
        private static async Task SeedItems(AppDbContext context)
        {
            var have = (await context.Items.Select(i => i.Key).ToListAsync()).ToHashSet();

            foreach (var definition in RecipeSeedData.Items)
            {
                if (!have.Add(definition.Key)) continue;

                context.Items.Add(new Item
                {
                    Key = definition.Key,
                    Name = definition.Name,
                    Description = definition.Description,
                    Kind = definition.Kind,
                    Slot = definition.Slot,
                    Modifier = definition.Modifier,
                    ModifierValue = definition.ModifierValue,
                    Tier = definition.Tier,
                });
            }
        }

        /// <summary>
        /// Recipes from the embedded JSON (§4.2). A recipe naming a material or item that
        /// does not exist is skipped rather than throwing — a typo in balance data should
        /// not stop the API booting.
        /// </summary>
        private static async Task SeedRecipes(AppDbContext context)
        {
            var have = (await context.Recipes.Select(r => r.Key).ToListAsync()).ToHashSet();

            var materialIds = await context.Materials.ToDictionaryAsync(m => m.Key, m => m.Id);
            var itemIds = await context.Items.ToDictionaryAsync(i => i.Key, i => i.Id);

            foreach (var definition in RecipeSeedData.Recipes)
            {
                if (have.Contains(definition.Key)) continue;

                int? outputMaterialId = definition.OutputMaterial is not null
                    && materialIds.TryGetValue(definition.OutputMaterial, out var mid) ? mid : null;

                int? outputItemId = definition.OutputItem is not null
                    && itemIds.TryGetValue(definition.OutputItem, out var iid) ? iid : null;

                if (outputMaterialId is null && outputItemId is null) continue;

                var inputs = new List<RecipeInput>();
                var inputsResolved = true;

                foreach (var input in definition.Inputs)
                {
                    if (!materialIds.TryGetValue(input.Material, out var inputId))
                    {
                        inputsResolved = false;
                        break;
                    }

                    inputs.Add(new RecipeInput { MaterialId = inputId, Quantity = input.Quantity });
                }

                if (!inputsResolved) continue;

                context.Recipes.Add(new Recipe
                {
                    Key = definition.Key,
                    Name = definition.Name,
                    Description = definition.Description,
                    SkillType = definition.Skill,
                    LevelRequired = definition.LevelRequired,
                    DurationSeconds = definition.DurationSeconds,
                    XpReward = definition.XpReward,
                    OutputMaterialId = outputMaterialId,
                    OutputItemId = outputItemId,
                    OutputQuantity = definition.OutputQuantity,
                    Inputs = inputs,
                });
            }
        }

        private static async Task SeedSetting(AppDbContext context, EnvironmentalSettingEnum key, string value)
        {
            var name = key.ToString();

            if (await context.EnvironmentalSettings.AnyAsync(s => s.Key == name)) return;

            await context.EnvironmentalSettings.AddAsync(new EnvironmentalSetting
            {
                Key = name,
                Value = value
            });
        }

        private static string Invariant(double value) =>
            value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
