using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Skills;

/// <summary>
/// Seeded skill definitions, terrain mappings and tier ladders (Stage 04).
///
/// <para><b>This is the template every later skill copies.</b> Adding Fishing or Mining
/// should mean adding rows here and nothing else — if a later skill needs C#, the
/// machinery was built too narrowly and the machinery is what should change.</para>
/// </summary>
public static class SkillSeedData
{
    /// <summary>
    /// The standard tier ladder from SKILL-TEMPLATE.md §1a: levels 1/10/20/35/50/70/90,
    /// gather seconds 3/5/9/15/24/40/60, XP 5/12/25/48/85/150/240.
    ///
    /// <para>XP/hour rises across this ladder (6,000 → 14,400) rather than staying flat.
    /// That is the prescribed shape and it satisfies §4.1a's actual requirement — a
    /// player is never <i>punished</i> for advancing — by rewarding them instead.</para>
    /// </summary>
    public static readonly (int Tier, int Level, double GatherSeconds, double XpPerUnit)[] StandardLadder =
    [
        (1,  1,  3,  5),
        (2, 10,  5, 12),
        (3, 20,  9, 25),
        (4, 35, 15, 48),
        (5, 50, 24, 85),
        (6, 70, 40, 150),
        (7, 90, 60, 240),
    ];

    /// <summary>Skill metadata for the skills screen.</summary>
    public static IReadOnlyList<SkillDefinition> Definitions { get; } = new List<SkillDefinition>
    {
        new()
        {
            SkillType = SkillType.Exploration,
            Name = "Exploration",
            Description = "Revealing new ground. Trained simply by going somewhere you have not been.",
            Icon = "🗺️",
            UnlockLevel = 1,
            Category = SkillCategory.Gathering,
        },
        new()
        {
            SkillType = SkillType.Foraging,
            Name = "Foraging",
            Description = "Gathering what grows wild. Best in woodland and farmland, but there is something to find almost anywhere.",
            Icon = "🌿",
            UnlockLevel = 1,
            Category = SkillCategory.Gathering,
        },
    };

    /// <summary>
    /// Which terrain trains which skill, and how well.
    ///
    /// <para>Every gathering skill <b>must</b> have an <see cref="TerrainType.Open"/> row.
    /// That row is what guarantees a player with no matching terrain still trains at base
    /// rate rather than not at all (§5.2) — terrain multiplies, it never gates.</para>
    /// </summary>
    public static IReadOnlyList<SkillTerrainMapping> TerrainMappings { get; } = new List<SkillTerrainMapping>
    {
        // ── Exploration: every cell is new ground, so every terrain trains it equally ──
        new() { SkillType = SkillType.Exploration, Terrain = TerrainType.Open,       XpPerCell = 2, YieldMultiplier = 1.0 },

        // ── Foraging ──────────────────────────────────────────────────────────────────
        // Woodland and farmland are best; everything else still trains at base rate.
        new() { SkillType = SkillType.Foraging, Terrain = TerrainType.Open,       XpPerCell = 1.0, YieldMultiplier = 1.0 },
        new() { SkillType = SkillType.Foraging, Terrain = TerrainType.Woodland,   XpPerCell = 3.0, YieldMultiplier = 2.0 },
        new() { SkillType = SkillType.Foraging, Terrain = TerrainType.Farmland,   XpPerCell = 3.0, YieldMultiplier = 2.0 },
        new() { SkillType = SkillType.Foraging, Terrain = TerrainType.Coastal,    XpPerCell = 1.5, YieldMultiplier = 1.25 },
        new() { SkillType = SkillType.Foraging, Terrain = TerrainType.Water,      XpPerCell = 1.5, YieldMultiplier = 1.25 },
        new() { SkillType = SkillType.Foraging, Terrain = TerrainType.Urban,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
        new() { SkillType = SkillType.Foraging, Terrain = TerrainType.Industrial, XpPerCell = 1.0, YieldMultiplier = 1.0 },
        new() { SkillType = SkillType.Foraging, Terrain = TerrainType.Rocky,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
    };

    /// <summary>
    /// Foraging's seven material tiers — the first full ladder in the game and the
    /// reference every later skill copies (Stage 04 task 3).
    /// </summary>
    public static IReadOnlyList<Material> ForagingMaterials { get; } = BuildForagingLadder();

    private static List<Material> BuildForagingLadder()
    {
        var names = new[]
        {
            "Wild Grass",
            "Common Herbs",
            "Berries",
            "Root Vegetables",
            "Rare Fungi",
            "Nightbloom",
            "Everleaf",
        };

        var keys = new[]
        {
            "wild_grass",
            "common_herbs",
            "berries",
            "root_vegetables",
            "rare_fungi",
            "nightbloom",
            "everleaf",
        };

        var materials = new List<Material>();

        foreach (var (tier, level, seconds, xp) in StandardLadder)
        {
            materials.Add(new Material
            {
                Key = keys[tier - 1],
                Name = names[tier - 1],
                Category = MaterialCategory.Foraged,
                Tier = tier,
                SkillType = SkillType.Foraging,
                LevelRequired = level,
                BaseGatherSeconds = seconds,
                XpPerUnit = xp,

                // Higher tiers are rarer, so a smaller stack still represents real effort
                // and the cap bites at a comparable amount of gathering time.
                StackCap = tier <= 2 ? 1000 : tier <= 4 ? 500 : 250,
                DustPerOverflow = tier,
            });
        }

        return materials;
    }

    /// <summary>
    /// Drop entries for the Foraging ladder, by terrain.
    ///
    /// Derived from <see cref="TerrainMappings"/> rather than listed separately, so a
    /// terrain added above cannot be forgotten here.
    /// </summary>
    public static IEnumerable<(TerrainType Terrain, string MaterialKey, int Weight, int Min, int Max)> DropEntries()
    {
        var foragingTerrains = TerrainMappings
            .Where(m => m.SkillType == SkillType.Foraging)
            .Select(m => m.Terrain)
            .Distinct();

        foreach (var terrain in foragingTerrains)
        {
            foreach (var material in ForagingMaterials)
            {
                // Lower tiers are commoner within a terrain. Tier selection in DropRoller
                // still prefers the highest unlocked band, so this only shapes the
                // fallback mix rather than capping what is reachable.
                var weight = Math.Max(1, 10 - (material.Tier - 1) * 2);

                var max = material.Tier <= 2 ? 3 : material.Tier <= 4 ? 2 : 1;

                yield return (terrain, material.Key, weight, 1, max);
            }
        }
    }
}
