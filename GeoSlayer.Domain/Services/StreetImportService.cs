using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using Newtonsoft.Json.Linq;
using Serilog;

namespace GeoSlayer.Domain.Services;

/// <summary>
/// On-demand + scheduled import of OSM street data.
/// The world is divided into grid cells of <see cref="CellSize"/> degrees
/// (~5 km).  Streets are fetched from the Overpass API per-cell and only
/// when a player walks into a cell that hasn't been loaded yet.
/// A weekly Hangfire job refreshes cells older than <see cref="RefreshAgeDays"/>.
/// </summary>
public class StreetImportService(IServiceScopeFactory scopeFactory, PoiImportService poiImportService)
{
    // ── Grid settings ──────────────────────────────────────────────
    /// <summary>
    /// Cell size in degrees.  0.05° ≈ 5.5 km at the equator.
    /// Gives a good balance: small enough for fast Overpass queries,
    /// large enough that players don't trigger new imports every block.
    /// </summary>
    public const double CellSize = 0.05;

    /// <summary>How old a cell can be before the refresh job re-fetches it.</summary>
    private const int RefreshAgeDays = 30;

    private const string OverpassUrl = "https://overpass-api.de/api/interpreter";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    // ── Public API ────────────────────────────────────────────────

    /// <summary>
    /// Ensures the grid cell containing the given coordinate has been
    /// imported.  If it already exists (and isn't stale), this is a no-op.
    /// Returns quickly so it can be called in the hot sync path.
    /// </summary>
    public async Task EnsureCellLoadedAsync(double latitude, double longitude)
    {
        var cellLat = SnapToGrid(latitude);
        var cellLng = SnapToGrid(longitude);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var region = await db.ImportedRegions
            .FirstOrDefaultAsync(r => r.CellLat == cellLat && r.CellLng == cellLng);

        if (region is not null)
            return; // already loaded — the refresh job handles staleness

        await ImportCellAsync(cellLat, cellLng);

        // Also load POIs for this cell
        await poiImportService.ImportCellPoisAsync(cellLat, cellLng);
    }

    /// <summary>
    /// Hangfire recurring job: re-imports any cells older than
    /// <see cref="RefreshAgeDays"/> so road changes get picked up.
    /// Also handles initial imports for adjacent cells around active
    /// players so streets are ready before they walk there.
    /// </summary>
    public async Task RefreshStaleCellsAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-RefreshAgeDays);
        var staleCells = await db.ImportedRegions
            .Where(r => r.ImportedAtUtc < cutoff)
            .OrderBy(r => r.ImportedAtUtc)
            .Take(50) // cap per run to avoid Overpass rate limits
            .ToListAsync();

        Log.Information("StreetImport refresh: {Count} stale cells to update", staleCells.Count);

        foreach (var cell in staleCells)
        {
            try
            {
                await ImportCellAsync(cell.CellLat, cell.CellLng);
                await poiImportService.ImportCellPoisAsync(cell.CellLat, cell.CellLng);
                // Small delay to respect Overpass rate limits
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "StreetImport refresh: failed cell ({Lat},{Lng})",
                    cell.CellLat, cell.CellLng);
            }
        }

        // Pre-load adjacent cells for areas with active players
        await PreloadAdjacentCellsAsync(db);
    }

    // ── Internals ─────────────────────────────────────────────────

    /// <summary>
    /// Imports a single grid cell: fetches from Overpass, upserts
    /// streets, and marks the cell as imported.
    /// </summary>
    private async Task ImportCellAsync(double cellLat, double cellLng)
    {
        var south = cellLat;
        var west = cellLng;
        var north = cellLat + CellSize;
        var east = cellLng + CellSize;

        Log.Information("StreetImport: loading cell ({CellLat},{CellLng})", cellLat, cellLng);

        var query = BuildOverpassQuery(south, west, north, east);

        JObject json;
        try
        {
            json = await FetchOverpassData(query);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "StreetImport: Overpass request failed for cell ({CellLat},{CellLng})",
                cellLat, cellLng);
            return;
        }

        var elements = json["elements"] as JArray;
        var streets = ParseStreets(elements);

        // Upsert streets
        int inserted = 0, updated = 0;
        const int batchSize = 500;

        for (int i = 0; i < streets.Count; i += batchSize)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var batch = streets.Skip(i).Take(batchSize).ToList();
            var osmIds = batch.Select(s => s.OsmId).ToList();

            var existing = await db.Streets
                .Where(s => osmIds.Contains(s.OsmId))
                .ToDictionaryAsync(s => s.OsmId);

            foreach (var street in batch)
            {
                if (existing.TryGetValue(street.OsmId, out var found))
                {
                    found.Name = street.Name;
                    found.Path = street.Path;
                    updated++;
                }
                else
                {
                    db.Streets.Add(street);
                    inserted++;
                }
            }

            await db.SaveChangesAsync();
        }

        // Mark cell as imported
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var region = await db.ImportedRegions
                .FirstOrDefaultAsync(r => r.CellLat == cellLat && r.CellLng == cellLng);

            if (region is null)
            {
                db.ImportedRegions.Add(new ImportedRegion
                {
                    CellLat = cellLat,
                    CellLng = cellLng,
                    ImportedAtUtc = DateTime.UtcNow,
                    StreetCount = streets.Count,
                });
            }
            else
            {
                region.ImportedAtUtc = DateTime.UtcNow;
                region.StreetCount = streets.Count;
            }

            await db.SaveChangesAsync();
        }

        Log.Information(
            "StreetImport: cell ({CellLat},{CellLng}) done — {Inserted} inserted, {Updated} updated, {Total} total",
            cellLat, cellLng, inserted, updated, streets.Count);
    }

    /// <summary>
    /// Looks at where active players are and pre-loads the 8 adjacent
    /// cells around each of their current cells so streets are ready
    /// before they walk there.
    /// </summary>
    private async Task PreloadAdjacentCellsAsync(AppDbContext db)
    {
        // Find distinct cells that have active players (moved in last 24 h)
        var playerLocations = await db.Players
            .Select(p => p.Location)
            .ToListAsync();

        var cellsToCheck = new HashSet<(double lat, double lng)>();

        foreach (var loc in playerLocations)
        {
            if (loc is null) continue;
            var baseLat = SnapToGrid(loc.Y);
            var baseLng = SnapToGrid(loc.X);

            // Current cell + 8 neighbors
            for (int dLat = -1; dLat <= 1; dLat++)
            for (int dLng = -1; dLng <= 1; dLng++)
            {
                cellsToCheck.Add((
                    Math.Round(baseLat + dLat * CellSize, 6),
                    Math.Round(baseLng + dLng * CellSize, 6)));
            }
        }

        // Filter out cells already imported
        var existingCells = (await db.ImportedRegions.ToListAsync())
            .Select(r => (r.CellLat, r.CellLng))
            .ToHashSet();

        var missing = cellsToCheck.Except(existingCells).Take(20).ToList();

        Log.Information("StreetImport: pre-loading {Count} adjacent cells for active players",
            missing.Count);

        foreach (var (lat, lng) in missing)
        {
            try
            {
                await ImportCellAsync(lat, lng);
                await poiImportService.ImportCellPoisAsync(lat, lng);
                await Task.Delay(2000); // rate limit
            }
            catch (Exception ex)
            {
                Log.Error(ex, "StreetImport: preload failed for cell ({Lat},{Lng})", lat, lng);
            }
        }
    }

    /// <summary>Snaps a coordinate to the south-west corner of its grid cell.</summary>
    public static double SnapToGrid(double value)
    {
        return Math.Floor(value / CellSize) * CellSize;
    }

    private static List<Street> ParseStreets(JArray? elements)
    {
        var streets = new List<Street>();
        if (elements is null) return streets;

        foreach (var el in elements)
        {
            if (el["type"]?.ToString() != "way") continue;

            var osmId = el["id"]!.Value<long>();
            var tags = el["tags"] as JObject;
            var name = tags?["name"]?.ToString()
                    ?? tags?["ref"]?.ToString()
                    ?? "Unnamed";

            var geometry = el["geometry"] as JArray;
            if (geometry is null || geometry.Count < 2) continue;

            var coords = geometry
                .Select(g => new Coordinate(g["lon"]!.Value<double>(), g["lat"]!.Value<double>()))
                .ToArray();

            var line = new LineString(coords) { SRID = 4326 };

            streets.Add(new Street
            {
                OsmId = osmId,
                Name = name,
                Path = line,
            });
        }

        return streets;
    }

    private static string BuildOverpassQuery(double south, double west, double north, double east)
    {
        return $"""
            [out:json][timeout:300];
            way["highway"~"^(residential|tertiary|secondary|primary|trunk|motorway|unclassified|living_street|pedestrian|service|footway|cycleway|path|track)$"]({south},{west},{north},{east});
            out geom;
            """;
    }

    private static async Task<JObject> FetchOverpassData(string query)
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("data", query)
        });

        var response = await Http.PostAsync(OverpassUrl, content);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        return JObject.Parse(body);
    }
}
