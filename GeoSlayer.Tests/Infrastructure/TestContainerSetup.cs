using Testcontainers.PostgreSql;

// NUnit scopes a [SetUpFixture] to its own namespace and descendants only, so this
// must sit in the root test namespace to cover fixtures under GeoSlayer.Tests.Services.
namespace GeoSlayer.Tests
{
    /// <summary>
    /// Manages the lifecycle of the PostgreSQL (PostGIS) test container across all tests
    /// </summary>
    [SetUpFixture]
    public class TestContainerSetup
    {
        private static PostgreSqlContainer? _postgresContainer;
        private static string? _connectionString;

        /// <summary>
        /// Why the container could not start, or null if it did.  Kept so that a missing
        /// Docker daemon reports as "skipped, no Docker" rather than a wall of failures
        /// that look like broken code.
        /// </summary>
        public static string? UnavailableReason { get; private set; }

        /// <summary>True when a database-backed test can actually run.</summary>
        public static bool IsAvailable => _connectionString is not null;

        public static string ConnectionString
        {
            get
            {
                if (string.IsNullOrEmpty(_connectionString))
                {
                    throw new InvalidOperationException("Container has not been started. Call OneTimeSetUp first.");
                }
                return _connectionString;
            }
        }

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            try
            {
                await InitializeContainerAsync();
            }
            catch (Exception ex)
            {
                // Do not throw: that fails every integration test with an unrelated stack
                // trace.  Record the reason and let the base class skip them instead.
                UnavailableReason = ex.Message;
                Console.WriteLine($"Test container unavailable, database tests will be skipped: {ex.Message}");
            }
        }

        private static async Task InitializeContainerAsync()
        {
            // PostGIS image is required as the schema uses the postgis extension
            _postgresContainer = new PostgreSqlBuilder("postgis/postgis:16-3.4")
                .WithDatabase("geoslayer_test_db")
                .WithUsername("test_user")
                .WithPassword("test_password")
                .WithCleanUp(true)
                .Build();

            await _postgresContainer.StartAsync();
            _connectionString = _postgresContainer.GetConnectionString();

            Console.WriteLine($"Test container started with connection string: {_connectionString}");
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            if (_postgresContainer != null)
            {
                await _postgresContainer.StopAsync();
                await _postgresContainer.DisposeAsync();
                Console.WriteLine("Test container stopped and disposed");
            }
        }
    }
}
