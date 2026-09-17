using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Retention;

/// <summary>
/// Seeded District definitions (DESIGN.md §5.5).
///
/// <para>"New ones cost nothing but data" — so these are rows, not code. Each requires
/// <b>varied</b> terrain, which is what stops the optimal play being nine identical cells
/// of your best ground.</para>
/// </summary>
public static class RetentionSeedData
{
    public static IReadOnlyList<DistrictDefinition> Districts { get; } = new List<DistrictDefinition>
    {
        new()
        {
            Key = "homestead",
            Name = "Homestead",
            Description = "Woodland, water and farmland together — everything a settlement needs.",
            RequiredTerrains = TerrainType.Woodland | TerrainType.Water | TerrainType.Farmland,
            MinimumClaims = 3,
            OutputBonus = 0.15,
        },
        new()
        {
            Key = "riverside",
            Name = "Riverside",
            Description = "Where the woods meet the water.",
            RequiredTerrains = TerrainType.Woodland | TerrainType.Water,
            MinimumClaims = 2,
            OutputBonus = 0.08,
        },
        new()
        {
            Key = "quarry_town",
            Name = "Quarry Town",
            Description = "Stone and industry side by side.",
            RequiredTerrains = TerrainType.Rocky | TerrainType.Industrial,
            MinimumClaims = 2,
            OutputBonus = 0.10,
        },
        new()
        {
            Key = "market_town",
            Name = "Market Town",
            Description = "Farmland feeding a town.",
            RequiredTerrains = TerrainType.Farmland | TerrainType.Urban,
            MinimumClaims = 2,
            OutputBonus = 0.08,
        },
        new()
        {
            Key = "harbour",
            Name = "Harbour",
            Description = "Coast, water and somewhere to sell the catch.",
            RequiredTerrains = TerrainType.Coastal | TerrainType.Water | TerrainType.Urban,
            MinimumClaims = 3,
            OutputBonus = 0.15,
        },
        new()
        {
            Key = "the_shire",
            Name = "The Shire",
            Description = "Five kinds of ground in one holding. Rare, and worth it.",
            RequiredTerrains = TerrainType.Woodland | TerrainType.Water | TerrainType.Farmland
                             | TerrainType.Urban | TerrainType.Rocky,
            MinimumClaims = 4,
            OutputBonus = 0.25,
        },
    };
}
