using Testcontainers.PostgreSql;

namespace GeoSlayer.Tests.Infrastructure
{
    /// <summary>
    /// Manages the lifecycle of the PostgreSQL (PostGIS) test container across all tests
    /// </summary>
    [SetUpFixture]
    public class TestContainerSetup
    {
        private static PostgreSqlContainer? _postgresContainer;
        private static string? _connectionString;

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
            await InitializeContainerAsync();
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
