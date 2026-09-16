using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GeoSlayer.Domain.Database.Context;

/// <summary>
/// Design-time context for `dotnet ef migrations`.
///
/// Without this, EF builds the API host to find a context — and in a Debug build that
/// host starts a Testcontainers Postgres instance, so generating a migration would need
/// Docker running. Migrations only need the model and the provider, never a live
/// database, so this supplies exactly that.
///
/// The connection string is never opened; it only tells Npgsql which provider to target.
/// Set <c>GeoSlayerLive</c> if you want to point tooling at a real database.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string PlaceholderConnection =
        "Host=localhost;Database=geoslayer;Username=postgres;Password=postgres";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("GeoSlayerLive") ?? PlaceholderConnection;

        // No lazy-loading proxies: they affect runtime navigation loading, not the
        // schema, and the package is not referenced from this project.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, o => o.UseNetTopologySuite())
            .Options;

        return new AppDbContext(options);
    }
}
