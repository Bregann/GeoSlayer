using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Interfaces.Helpers;
using GeoSlayer.Domain.Services.Progression;
using Microsoft.AspNetCore.Identity;
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
