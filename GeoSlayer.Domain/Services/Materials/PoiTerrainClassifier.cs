using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Services.Materials
{
    /// <summary>
    /// Classifies a cell from the POIs already imported for it (Stage 03 task 3).
    ///
    /// <para>The stage asks to extend the Overpass import rather than add a second import
    /// path. This goes one step further and adds <b>no</b> new fetch at all: the existing
    /// query in <see cref="PoiImportService"/> already pulls the terrain-bearing tags
    /// (<c>natural</c>, <c>landuse</c>, <c>water</c>, <c>waterway</c>, <c>man_made</c>), and
    /// those rows are already stored with locations. Reading them back is cheaper than a
    /// second round trip, cannot rate-limit, and keeps one import path.</para>
    ///
    /// <para>The trade-off: resolution is limited to the POI rows OSM gave us, so a cell in
    /// the middle of a forest with no tagged feature classifies as <see cref="TerrainType.Open"/>.
    /// That is the safe direction to be wrong in — Open still yields base materials, so the
    /// player sees reduced yield, never a dead cell.</para>
    /// </summary>
    public class PoiTerrainClassifier(AppDbContext db) : ITerrainClassifier
    {
        /// <summary>
        /// Which skill a POI maps to implies what the ground is like. Indirect, but it reuses
        /// the mapping the import already applies rather than duplicating tag parsing.
        /// </summary>
        private static readonly Dictionary<SkillType, TerrainType> SkillTerrain = new()
        {
            [SkillType.Woodcutting] = TerrainType.Woodland,
            [SkillType.Fishing] = TerrainType.Water,
            [SkillType.Farming] = TerrainType.Farmland,
            [SkillType.Mining] = TerrainType.Rocky,
            [SkillType.Smithing] = TerrainType.Industrial,

            // Everyday amenities are what "urban" means here — shops, banks, pubs, libraries.
            [SkillType.Trading] = TerrainType.Urban,
            [SkillType.Banking] = TerrainType.Urban,
            [SkillType.Tavern] = TerrainType.Urban,
            [SkillType.Knowledge] = TerrainType.Urban,
            [SkillType.Cooking] = TerrainType.Urban,
            [SkillType.Healing] = TerrainType.Urban,
            [SkillType.Prayer] = TerrainType.Urban,
            [SkillType.Athletics] = TerrainType.Urban,
        };

        public async Task<TerrainType> Classify(int gridLat, int gridLng, CancellationToken ct)
        {
            var (south, west, north, east) = FogService.CellBounds(gridLat, gridLng);

            // Bounding-box filter on the indexed geometry column. The POI table has a GiST
            // index on Location, so this stays cheap even in a dense city.
            var skills = await db.PointsOfInterest
                .Where(p => p.Location.Y >= south && p.Location.Y < north
                         && p.Location.X >= west && p.Location.X < east)
                .Select(p => p.Skill)
                .Distinct()
                .ToListAsync(ct);

            var terrain = TerrainType.Open;

            foreach (var skill in skills)
            {
                if (SkillTerrain.TryGetValue(skill, out var flag))
                    terrain |= flag;
            }

            return terrain;
        }
    }
}
