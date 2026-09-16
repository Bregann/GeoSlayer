using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Interfaces.Helpers;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Services.Crafting;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Domain.Services.Skills;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GeoSlayer.Tests.Infrastructure
{
    /// <summary>
    /// Reusable, minimal seed data for tests. Add new seed methods here as features get tests.
    /// </summary>
    public static class TestDatabaseSeedHelper
    {
        private static readonly PasswordHasher<User> _passwordHasher = new();

        public static async Task<User> SeedTestUser(AppDbContext context, string username = "testuser", string password = "test123!")
        {
            var user = new User
            {
                Username = username,
                FirstName = "Test",
                Email = $"{username}@test.com",
                PasswordHash = _passwordHasher.HashPassword(new User(), password)
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        public static async Task<Player> SeedTestPlayer(AppDbContext context, User user, double latitude = 51.5074, double longitude = -0.1278)
        {
            var player = new Player
            {
                UserId = user.Id,
                LastLatitude = latitude,
                LastLongitude = longitude
            };

            context.Players.Add(player);
            await context.SaveChangesAsync();
            return player;
        }

        /// <summary>
        /// Seeds a user with a linked player - the common starting point for most tests
        /// </summary>
        public static async Task<(User User, Player Player)> SeedMinimalData(AppDbContext context)
        {
            var user = await SeedTestUser(context);
            var player = await SeedTestPlayer(context, user);
            return (user, player);
        }

        /// <summary>
        /// Seeds the unlock ladder and upgrade tree (DESIGN.md §3.0a, §3.1).  Tests that
        /// touch progression need these rows: without the ladder nothing unlocks, and
        /// without the tree no upgrade can be bought.
        /// </summary>
        public static async Task SeedProgressionDefinitions(AppDbContext context)
        {
            foreach (var rung in ProgressionSeedData.Ladder)
            {
                context.UnlockDefinitions.Add(new UnlockDefinition
                {
                    AdventurerLevel = rung.AdventurerLevel,
                    UnlockType = rung.UnlockType,
                    Payload = rung.Payload,
                    DisplayName = rung.DisplayName,
                });
            }

            foreach (var upgrade in ProgressionSeedData.Upgrades)
            {
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

            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Seeds the material pools and terrain drop tables (§4.1, §4.1a, §7.4).
        /// </summary>
        public static async Task SeedMaterialDefinitions(AppDbContext context)
        {
            foreach (var material in MaterialSeedData.Materials)
            {
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

            await context.SaveChangesAsync();

            var ids = await context.Materials.ToDictionaryAsync(m => m.Key, m => m.Id);

            var seen = new HashSet<(TerrainType Terrain, int MaterialId)>();

            foreach (var (terrain, key, weight, min, max) in MaterialSeedData.DropEntries())
            {
                if (!ids.TryGetValue(key, out var materialId)) continue;
                if (!seen.Add((terrain, materialId))) continue;

                context.DropTableEntries.Add(new DropTableEntry
                {
                    Terrain = terrain,
                    MaterialId = materialId,
                    Weight = weight,
                    MinQuantity = min,
                    MaxQuantity = max,
                });
            }

            await context.SaveChangesAsync();
        }

        /// <summary>
        /// A <see cref="MaterialService"/> backed by a <b>fake</b> terrain classifier.
        ///
        /// Nothing in the suite may reach Overpass: a test that depends on a live third
        /// party is not a test. <paramref name="terrain"/> is what every cell classifies
        /// as, so a fixture can pin the geography it needs.
        /// </summary>
        public static MaterialService CreateMaterialService(
            AppDbContext context, TerrainType terrain = TerrainType.Open)
        {
            var classifier = new Mock<ITerrainClassifier>();

            classifier
                .Setup(c => c.Classify(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(terrain);

            return new MaterialService(context, classifier.Object);
        }

        /// <summary>
        /// Seeds skill definitions, terrain mappings and the Foraging tier ladder
        /// (Stage 04). Call after <see cref="SeedMaterialDefinitions"/>.
        /// </summary>
        public static async Task SeedSkillDefinitions(AppDbContext context)
        {
            foreach (var definition in SkillSeedData.Definitions)
            {
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

            foreach (var mapping in SkillSeedData.TerrainMappings)
            {
                context.SkillTerrainMappings.Add(new SkillTerrainMapping
                {
                    SkillType = mapping.SkillType,
                    Terrain = mapping.Terrain,
                    XpPerCell = mapping.XpPerCell,
                    YieldMultiplier = mapping.YieldMultiplier,
                });
            }

            foreach (var material in SkillSeedData.ForagingMaterials)
            {
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

            await context.SaveChangesAsync();

            var ids = await context.Materials.ToDictionaryAsync(m => m.Key, m => m.Id);

            var seen = (await context.DropTableEntries
                    .Select(e => new { e.Terrain, e.MaterialId })
                    .ToListAsync())
                .Select(e => (e.Terrain, e.MaterialId))
                .ToHashSet();

            foreach (var (terrain, key, weight, min, max) in SkillSeedData.DropEntries())
            {
                if (!ids.TryGetValue(key, out var materialId)) continue;
                if (!seen.Add((terrain, materialId))) continue;

                context.DropTableEntries.Add(new DropTableEntry
                {
                    Terrain = terrain,
                    MaterialId = materialId,
                    Weight = weight,
                    MinQuantity = min,
                    MaxQuantity = max,
                });
            }

            await context.SaveChangesAsync();
        }

        /// <summary>
        /// A real <see cref="SkillTrainingService"/> over the test database, with terrain
        /// supplied by a fake classifier.
        /// </summary>
        public static SkillTrainingService CreateSkillTrainingService(
            AppDbContext context,
            ProgressionService progression,
            MaterialService materials) =>
            new(context, progression, materials, CreateCraftingService(context, progression, materials));

        /// <summary>A real <see cref="CraftingService"/> over the test database.</summary>
        public static CraftingService CreateCraftingService(
            AppDbContext context,
            ProgressionService progression,
            MaterialService materials) =>
            new(context, progression, materials);

        /// <summary>
        /// Seeds items and recipes from the embedded JSON (§4.2, §4.3). Call after
        /// <see cref="SeedMaterialDefinitions"/> and <see cref="SeedSkillDefinitions"/>.
        /// </summary>
        public static async Task SeedCraftingDefinitions(AppDbContext context)
        {
            foreach (var definition in RecipeSeedData.Items)
            {
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

            await context.SaveChangesAsync();

            var materialIds = await context.Materials.ToDictionaryAsync(m => m.Key, m => m.Id);
            var itemIds = await context.Items.ToDictionaryAsync(i => i.Key, i => i.Id);

            foreach (var definition in RecipeSeedData.Recipes)
            {
                int? outputMaterialId = definition.OutputMaterial is not null
                    && materialIds.TryGetValue(definition.OutputMaterial, out var mid) ? mid : null;

                int? outputItemId = definition.OutputItem is not null
                    && itemIds.TryGetValue(definition.OutputItem, out var iid) ? iid : null;

                if (outputMaterialId is null && outputItemId is null) continue;

                var inputs = new List<RecipeInput>();
                var resolved = true;

                foreach (var input in definition.Inputs)
                {
                    if (!materialIds.TryGetValue(input.Material, out var inputId))
                    {
                        resolved = false;
                        break;
                    }

                    inputs.Add(new RecipeInput { MaterialId = inputId, Quantity = input.Quantity });
                }

                if (!resolved) continue;

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

            await context.SaveChangesAsync();
        }

        /// <summary>
        /// A real <see cref="ProgressionService"/> over the test database, with settings
        /// falling back to <see cref="ProgressionDefaults"/> — the values the game ships.
        /// </summary>
        public static ProgressionService CreateProgressionService(AppDbContext context)
        {
            var settings = new Mock<IEnvironmentalSettingHelper>();

            // Return null for every key so the service uses its documented defaults,
            // rather than a sentinel string that would silently parse as garbage.
            settings.Setup(x => x.TryGetEnviromentalSettingValue(It.IsAny<EnvironmentalSettingEnum>()))
                    .Returns((string?)null);

            return new ProgressionService(context, settings.Object);
        }
    }
}
