# GeoSlayer Test Infrastructure Guide

## Overview
This test project uses **NUnit**, **Testcontainers**, and **Moq** to run integration tests against a real PostgreSQL + PostGIS database.

Docker must be running to execute the tests.

## Infrastructure Components

### `TestContainerSetup.cs`
- `[SetUpFixture]` - runs once for the entire test suite
- Starts a `postgis/postgis:16-3.4` container (PostGIS is required by the schema) and cleans it up afterwards
- Connection string is available via `TestContainerSetup.ConnectionString`

### `DatabaseIntegrationTestBase.cs`
- Base class for all integration tests requiring a database
- Creates a **fresh database for each test** using **migrations** (not `EnsureCreated`)
- Configures the context the same way as `Program.cs`: Npgsql + NetTopologySuite + lazy loading proxies
- Exposes a `DbContext` property
- Override `CustomSetUp()` / `CustomTearDown()` for fixture-specific setup

### `TestDatabaseSeedHelper.cs`
Minimal, focused seed data:
- `SeedTestUser()` - create a user
- `SeedTestPlayer()` - create a player linked to a user
- `SeedMinimalData()` - user + player

Add new seed methods here as features get tests.

### `MockFactory.cs`
Mocks for external dependencies (never mock the database):
- `CreateHttpContextAccessor()` - HTTP context with user claims
- `CreateUserContextHelper()` - user context helper (pass a seeded `User` to have `GetUser()` return it)
- `CreateEnvironmentalSettingHelper()` - environmental settings
- `CreateMockHttpClient()` - HttpClient returning a canned response

## Writing Integration Tests

Place tests in folders mirroring the domain, e.g. `Services/Fog/FogServiceIntegrationTests.cs`.

```csharp
[TestFixture]
public class FogServiceIntegrationTests : DatabaseIntegrationTestBase
{
    private FogService _sut = null!;
    private Player _player = null!;

    protected override async Task CustomSetUp()
    {
        (_, _player) = await TestDatabaseSeedHelper.SeedMinimalData(DbContext);
        _sut = new FogService(DbContext);
    }

    [Test]
    public async Task MethodName_Scenario_ExpectedResult()
    {
        // Arrange
        // Act
        // Assert
        Assert.That(result, Is.Not.Null);
    }
}
```

## Best Practices
- Each test gets a fresh database - don't rely on data from other tests or test order
- Seed only the minimum data a test needs
- Mock external dependencies (HTTP APIs, Overpass/OSM, etc.), not EF Core
- Name tests `MethodName_Scenario_ExpectedResult`
- Use NUnit constraint assertions: `Assert.That(actual, Is.EqualTo(expected))`
- Verify persisted state via `DbContext` after the action

## Running Tests

```bash
# Run all tests
dotnet test

# Run a specific test class
dotnet test --filter "FullyQualifiedName~FogServiceIntegrationTests"

# Run tests matching a name
dotnet test --filter "Name~Reveal"
```

## Troubleshooting
- **Container won't start** - make sure Docker is running and the `postgis/postgis` image can be pulled
- **`type "geometry" does not exist`** - the postgis extension is created by the migrations; make sure the test inherits `DatabaseIntegrationTestBase`
- **Lazy loading not working** - navigation properties must be `virtual`, and migrations (not `EnsureCreated`) must be used
