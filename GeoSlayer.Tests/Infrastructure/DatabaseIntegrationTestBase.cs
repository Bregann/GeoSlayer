using GeoSlayer.Domain.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Tests.Infrastructure
{
    /// <summary>
    /// Base class for integration tests that need a real database
    /// Automatically creates and tears down the database for each test
    /// </summary>
    public abstract class DatabaseIntegrationTestBase
    {
        protected AppDbContext DbContext { get; private set; } = null!;

        [SetUp]
        public async Task SetUp()
        {
            // No Docker means these cannot run.  Skip with the real reason rather than
            // failing — a missing daemon is not a broken test.
            if (!TestContainerSetup.IsAvailable)
            {
                Assert.Ignore(
                    "Database tests need Docker for Testcontainers. " +
                    $"Container unavailable: {TestContainerSetup.UnavailableReason ?? "not started"}");
            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(TestContainerSetup.ConnectionString, o => o.UseNetTopologySuite())
                .UseLazyLoadingProxies()
                .EnableSensitiveDataLogging()
                .Options;

            DbContext = new AppDbContext(options);

            // Ensure database is created fresh for each test
            await DbContext.Database.EnsureDeletedAsync();

            // Use MigrateAsync instead of EnsureCreatedAsync to properly configure lazy loading proxies
            await DbContext.Database.MigrateAsync();

            // Npgsql caches type info per connection, so reload after the postgis extension is created
            var connection = (Npgsql.NpgsqlConnection)DbContext.Database.GetDbConnection();
            await connection.OpenAsync();
            await connection.ReloadTypesAsync();
            await connection.CloseAsync();

            // Custom setup for derived classes
            await CustomSetUp();
        }

        [TearDown]
        public async Task TearDown()
        {
            // SetUp may have been skipped before the context existed.
            if (DbContext is null)
            {
                return;
            }

            // Custom teardown for derived classes
            await CustomTearDown();

            await DbContext.DisposeAsync();
        }

        /// <summary>
        /// Override this method to add custom setup logic
        /// </summary>
        protected virtual Task CustomSetUp()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Override this method to add custom teardown logic
        /// </summary>
        protected virtual Task CustomTearDown()
        {
            return Task.CompletedTask;
        }
    }
}
