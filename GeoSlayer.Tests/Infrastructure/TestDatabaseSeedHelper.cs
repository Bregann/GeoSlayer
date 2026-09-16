using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using Microsoft.AspNetCore.Identity;

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
    }
}
