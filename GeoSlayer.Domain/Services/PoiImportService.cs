using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using Newtonsoft.Json.Linq;
using Serilog;

namespace GeoSlayer.Domain.Services;

/// <summary>
/// Imports Points of Interest from OpenStreetMap via the Overpass API
/// and maps them to game skills.  Uses the same grid-cell system as
/// <see cref="StreetImportService"/>.
/// </summary>
public class PoiImportService(IServiceScopeFactory scopeFactory)
{
    private const string OverpassUrl = "https://overpass-api.de/api/interpreter";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    // ────────────────────────────────────────────────────────────────
    //  OSM tag → SkillType mapping
    // ────────────────────────────────────────────────────────────────
    // Each entry is (osmKey, osmValue pattern, skill, xpReward).
    // Value "*" means any value for that key matches.
    // Evaluated top-to-bottom; first match wins.
    // ────────────────────────────────────────────────────────────────

    private static readonly (string key, string value, SkillType skill, int xp)[] TagMappings =
    [
        // ── Prayer ──────────────────────────────────────────────
        ("amenity",  "place_of_worship",  SkillType.Prayer,      15),
        ("building", "church",            SkillType.Prayer,      15),
        ("building", "cathedral",         SkillType.Prayer,      25),
        ("building", "chapel",            SkillType.Prayer,      10),
        ("building", "mosque",            SkillType.Prayer,      15),
        ("building", "temple",            SkillType.Prayer,      15),
        ("building", "synagogue",         SkillType.Prayer,      15),

        // ── Knowledge ───────────────────────────────────────────
        ("amenity",  "library",           SkillType.Knowledge,   15),
        ("amenity",  "school",            SkillType.Knowledge,   10),
        ("amenity",  "university",        SkillType.Knowledge,   20),
        ("amenity",  "college",           SkillType.Knowledge,   15),
        ("tourism",  "museum",            SkillType.Knowledge,   20),
        ("shop",     "books",             SkillType.Knowledge,   10),

        // ── Woodcutting ─────────────────────────────────────────
        ("natural",  "wood",              SkillType.Woodcutting, 10),
        ("landuse",  "forest",            SkillType.Woodcutting, 10),
        ("leisure",  "nature_reserve",    SkillType.Woodcutting, 15),

        // ── Fishing ─────────────────────────────────────────────
        ("leisure",  "fishing",           SkillType.Fishing,     15),
        ("man_made", "pier",              SkillType.Fishing,     10),
        ("natural",  "water",             SkillType.Fishing,     10),
        ("water",    "lake",              SkillType.Fishing,     10),
        ("water",    "pond",              SkillType.Fishing,     10),
        ("waterway", "riverbank",         SkillType.Fishing,     10),
        ("harbour",  "*",                 SkillType.Fishing,     10),

        // ── Healing ─────────────────────────────────────────────
        ("amenity",  "hospital",          SkillType.Healing,     20),
        ("amenity",  "pharmacy",          SkillType.Healing,     10),
        ("amenity",  "clinic",            SkillType.Healing,     15),
        ("amenity",  "doctors",           SkillType.Healing,     10),
        ("amenity",  "veterinary",        SkillType.Healing,     10),

        // ── Athletics ───────────────────────────────────────────
        ("leisure",  "sports_centre",     SkillType.Athletics,   15),
        ("leisure",  "stadium",           SkillType.Athletics,   20),
        ("leisure",  "fitness_centre",    SkillType.Athletics,   15),
        ("leisure",  "swimming_pool",     SkillType.Athletics,   10),
        ("leisure",  "pitch",             SkillType.Athletics,   10),
        ("leisure",  "track",             SkillType.Athletics,   10),

        // ── Tavern ──────────────────────────────────────────────
        ("amenity",  "pub",               SkillType.Tavern,      10),
        ("amenity",  "bar",               SkillType.Tavern,      10),
        ("amenity",  "restaurant",        SkillType.Tavern,      10),
        ("amenity",  "cafe",              SkillType.Tavern,      10),
        ("amenity",  "nightclub",         SkillType.Tavern,      10),
        ("amenity",  "biergarten",        SkillType.Tavern,      10),

        // ── Trading ─────────────────────────────────────────────
        ("amenity",  "marketplace",       SkillType.Trading,     15),
        ("shop",     "supermarket",       SkillType.Trading,     10),
        ("shop",     "mall",              SkillType.Trading,     15),
        ("shop",     "department_store",  SkillType.Trading,     15),
        ("shop",     "general",           SkillType.Trading,     10),
        ("shop",     "convenience",       SkillType.Trading,     10),

        // ── Banking ─────────────────────────────────────────────
        ("amenity",  "bank",              SkillType.Banking,     10),
        ("amenity",  "atm",              SkillType.Banking,      5),
        ("amenity",  "post_office",       SkillType.Banking,     10),

        // ── Combat ──────────────────────────────────────────────
        ("historic", "castle",            SkillType.Combat,      25),
        ("historic", "fort",              SkillType.Combat,      20),
        ("historic", "ruins",             SkillType.Combat,      15),
        ("historic", "battlefield",       SkillType.Combat,      20),
        ("historic", "monument",          SkillType.Combat,      10),
        ("historic", "memorial",          SkillType.Combat,      10),
        ("military", "*",                 SkillType.Combat,      15),

        // ── Mining ──────────────────────────────────────────────
        ("man_made", "mineshaft",         SkillType.Mining,      20),
        ("landuse",  "quarry",            SkillType.Mining,      15),
        ("natural",  "cave_entrance",     SkillType.Mining,      15),
        ("geological","*",                SkillType.Mining,      10),

        // ── Farming ─────────────────────────────────────────────
        ("landuse",  "farmland",          SkillType.Farming,     10),
        ("landuse",  "allotments",        SkillType.Farming,     10),
        ("leisure",  "garden",            SkillType.Farming,     10),
        ("building", "farm",              SkillType.Farming,     10),
        ("building", "greenhouse",        SkillType.Farming,     10),
        ("shop",     "farm",              SkillType.Farming,     10),

        // ── Smithing ────────────────────────────────────────────
        ("craft",    "blacksmith",        SkillType.Smithing,    20),
        ("craft",    "metal_construction",SkillType.Smithing,    15),
        ("man_made", "works",             SkillType.Smithing,    10),
        ("landuse",  "industrial",        SkillType.Smithing,    10),

        // ── Cooking ─────────────────────────────────────────────
        ("shop",     "bakery",            SkillType.Cooking,     10),
        ("shop",     "butcher",           SkillType.Cooking,     10),
        ("shop",     "greengrocer",       SkillType.Cooking,     10),
        ("shop",     "deli",              SkillType.Cooking,     10),
        ("amenity",  "fast_food",         SkillType.Cooking,     10),

        // ── Exploration (fallback for interesting misc POIs) ────
        ("tourism",  "viewpoint",         SkillType.Exploration, 15),
        ("tourism",  "attraction",        SkillType.Exploration, 15),
        ("tourism",  "artwork",           SkillType.Exploration, 10),
        ("amenity",  "fountain",          SkillType.Exploration, 5),
        ("leisure",  "park",              SkillType.Exploration, 10),
    ];

    // ────────────────────────────────────────────────────────────────
    //  Public API
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Imports POIs for a single grid cell.
    /// Called alongside <see cref="StreetImportService.EnsureCellLoadedAsync"/>
    /// so POIs are ready when the player arrives.
    /// </summary>
    public async Task ImportCellPoisAsync(double cellLat, double cellLng)
    {
        var south = cellLat;
        var west = cellLng;
        var north = cellLat + StreetImportService.CellSize;
        var east = cellLng + StreetImportService.CellSize;

        Log.Information("PoiImport: loading cell ({CellLat},{CellLng})", cellLat, cellLng);

        var query = BuildOverpassQuery(south, west, north, east);

        JObject json;
        try
        {
            json = await FetchOverpassData(query);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PoiImport: Overpass request failed for cell ({CellLat},{CellLng})",
                cellLat, cellLng);
            return;
        }

        var elements = json["elements"] as JArray;
        var pois = ParsePois(elements);

        Log.Information("PoiImport: parsed {Count} POIs for cell ({CellLat},{CellLng})",
            pois.Count, cellLat, cellLng);

        // Upsert in batches
        const int batchSize = 500;
        int inserted = 0, updated = 0;

        for (int i = 0; i < pois.Count; i += batchSize)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var batch = pois.Skip(i).Take(batchSize).ToList();

            // Build lookup keys for existing check
            var batchKeys = batch.Select(p => new { p.OsmId, p.OsmType }).ToList();
            var osmIds = batchKeys.Select(k => k.OsmId).Distinct().ToList();

            var existing = await db.PointsOfInterest
                .Where(p => osmIds.Contains(p.OsmId))
                .ToDictionaryAsync(p => (p.OsmId, p.OsmType));

            foreach (var poi in batch)
            {
                if (existing.TryGetValue((poi.OsmId, poi.OsmType), out var found))
                {
                    found.Name = poi.Name;
                    found.Location = poi.Location;
                    found.Skill = poi.Skill;
                    found.XpReward = poi.XpReward;
                    updated++;
                }
                else
                {
                    db.PointsOfInterest.Add(poi);
                    inserted++;
                }
            }

            await db.SaveChangesAsync();
        }

        Log.Information(
            "PoiImport: cell ({CellLat},{CellLng}) done — {Inserted} inserted, {Updated} updated",
            cellLat, cellLng, inserted, updated);
    }

    // ────────────────────────────────────────────────────────────────
    //  Parsing
    // ────────────────────────────────────────────────────────────────

    private static List<PointOfInterest> ParsePois(JArray? elements)
    {
        var pois = new List<PointOfInterest>();
        if (elements is null) return pois;

        foreach (var el in elements)
        {
            var type = el["type"]?.ToString();
            if (type is not ("node" or "way")) continue;

            var osmId = el["id"]!.Value<long>();
            var tags = el["tags"] as JObject;
            if (tags is null) continue;

            // Try to match tags to a skill
            var match = MatchSkill(tags);
            if (match is null) continue;

            var (skill, xp) = match.Value;

            var name = tags["name"]?.ToString() ?? tags["ref"]?.ToString() ?? "";

            // Get location: nodes have lat/lon directly,
            // ways have a geometry array — use the centroid.
            Point location;
            if (type == "node")
            {
                var lat = el["lat"]!.Value<double>();
                var lon = el["lon"]!.Value<double>();
                location = new Point(lon, lat) { SRID = 4326 };
            }
            else
            {
                // Way — compute centroid from geometry nodes
                var geometry = el["geometry"] as JArray;
                if (geometry is null || geometry.Count == 0) continue;

                var coords = geometry
                    .Select(g => new Coordinate(
                        g["lon"]!.Value<double>(),
                        g["lat"]!.Value<double>()))
                    .ToArray();

                // Use centroid of the polygon/line
                if (coords.Length == 1)
                {
                    location = new Point(coords[0]) { SRID = 4326 };
                }
                else
                {
                    // Close the ring if it looks like a polygon
                    if (coords.First().Equals2D(coords.Last()) && coords.Length >= 4)
                    {
                        var polygon = new Polygon(
                            new LinearRing(coords)) { SRID = 4326 };
                        location = (Point)polygon.Centroid;
                        location.SRID = 4326;
                    }
                    else
                    {
                        var line = new LineString(coords) { SRID = 4326 };
                        location = (Point)line.Centroid;
                        location.SRID = 4326;
                    }
                }
            }

            pois.Add(new PointOfInterest
            {
                OsmId = osmId,
                OsmType = type,
                Name = name,
                Skill = skill,
                Location = location,
                XpReward = xp,
            });
        }

        return pois;
    }

    private static (SkillType skill, int xp)? MatchSkill(JObject tags)
    {
        foreach (var (key, value, skill, xp) in TagMappings)
        {
            var tagValue = tags[key]?.ToString();
            if (tagValue is null) continue;

            if (value == "*" || string.Equals(tagValue, value, StringComparison.OrdinalIgnoreCase))
                return (skill, xp);
        }

        return null;
    }

    // ────────────────────────────────────────────────────────────────
    //  Overpass query
    // ────────────────────────────────────────────────────────────────

    private static string BuildOverpassQuery(double south, double west, double north, double east)
    {
        // Pull nodes and ways that have any of the tags we care about.
        // Using a union of nwr (node/way/relation) queries for each key.
        return $"""
            [out:json][timeout:300];
            (
              nwr["amenity"~"place_of_worship|library|school|university|college|hospital|pharmacy|clinic|doctors|veterinary|pub|bar|restaurant|cafe|nightclub|biergarten|marketplace|bank|atm|post_office|fast_food|fountain"]({south},{west},{north},{east});
              nwr["tourism"~"museum|viewpoint|attraction|artwork"]({south},{west},{north},{east});
              nwr["leisure"~"fishing|sports_centre|stadium|fitness_centre|swimming_pool|pitch|track|nature_reserve|garden|park"]({south},{west},{north},{east});
              nwr["historic"~"castle|fort|ruins|battlefield|monument|memorial"]({south},{west},{north},{east});
              nwr["natural"~"wood|water|cave_entrance"]({south},{west},{north},{east});
              nwr["landuse"~"forest|quarry|farmland|allotments|industrial"]({south},{west},{north},{east});
              nwr["shop"~"supermarket|mall|department_store|general|convenience|books|bakery|butcher|greengrocer|deli|farm"]({south},{west},{north},{east});
              nwr["craft"~"blacksmith|metal_construction"]({south},{west},{north},{east});
              nwr["man_made"~"pier|mineshaft|works"]({south},{west},{north},{east});
              nwr["building"~"church|cathedral|chapel|mosque|temple|synagogue|farm|greenhouse"]({south},{west},{north},{east});
              nwr["military"]({south},{west},{north},{east});
              nwr["geological"]({south},{west},{north},{east});
              nwr["water"~"lake|pond"]({south},{west},{north},{east});
              nwr["waterway"="riverbank"]({south},{west},{north},{east});
              nwr["harbour"]({south},{west},{north},{east});
            );
            out center;
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
