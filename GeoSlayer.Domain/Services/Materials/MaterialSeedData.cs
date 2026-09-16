using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Materials;

/// <summary>
/// The seeded material pools and drop tables (DESIGN.md §4.1, §4.1a, §7.4).
///
/// <para><b>Pools, not bespoke items.</b> Each terrain category gets a tier ladder drawn
/// from a shared pool, rather than a unique item per POI type. Stage 03's budget is ≤25
/// distinct materials and ≤15 unique ones; this seeds <b>22</b> materials of which
/// <b>3</b> are unique, leaving room for later stages.</para>
///
/// <para><b>Tier economics.</b> <c>XpPerUnit</c> rises with <c>BaseGatherSeconds</c> so
/// XP/hour stays roughly flat — the ratio is held at 1 XP per 3 seconds across every tier,
/// so advancing is never a downgrade. Gather seconds follow the §4.1a shape
/// (3 / 5 / 9 / 15 / 24 …) and levels the 1 / 10 / 20 / 35 / 50 ladder.</para>
/// </summary>
public static class MaterialSeedData
{
    /// <summary>XP granted per second of gather time. Constant across tiers by design.</summary>
    public const double XpPerGatherSecond = 1.0 / 3.0;

    /// <summary>The universal filler and overflow currency (§7.4).</summary>
    public const string DustKey = "dust";

    private static Material Tiered(
        string key,
        string name,
        MaterialCategory category,
        int tier,
        int levelRequired,
        double gatherSeconds,
        SkillType? skill = null,
        bool isUnique = false,
        int stackCap = 1000)
        => new()
        {
            Key = key,
            Name = name,
            Category = category,
            Tier = tier,
            LevelRequired = levelRequired,
            BaseGatherSeconds = gatherSeconds,
            // Held proportional so XP/hour is flat across tiers (§4.1a).
            XpPerUnit = Math.Round(gatherSeconds * XpPerGatherSecond, 4),
            SkillType = skill,
            IsUnique = isUnique,
            StackCap = stackCap,
            DustPerOverflow = tier,
        };

    /// <summary>
    /// Every seeded material. Tiers follow the §4.1a ladder; the highest tiers are
    /// deliberately left for later stages rather than seeded empty here.
    /// </summary>
    public static IReadOnlyList<Material> Materials { get; } = new List<Material>
    {
        // ── Universal ────────────────────────────────────────────────
        // Dust is what overflow converts into and what an unclassified cell always has
        // available, so it must never be level-gated or capped tightly.
        new()
        {
            Key = DustKey,
            Name = "Dust",
            Category = MaterialCategory.Dust,
            Tier = 1,
            LevelRequired = 1,
            BaseGatherSeconds = 1,
            XpPerUnit = 0,          // Overflow must not become an XP source.
            StackCap = 1_000_000,
            DustPerOverflow = 1,
        },

        // ── Woodland (Woodcutting) ───────────────────────────────────
        Tiered("timber_rough",  "Rough Timber",  MaterialCategory.Woodland, 1, 1,  3,  SkillType.Woodcutting),
        Tiered("timber_oak",    "Oak Timber",    MaterialCategory.Woodland, 2, 10, 5,  SkillType.Woodcutting),
        Tiered("timber_yew",    "Yew Timber",    MaterialCategory.Woodland, 3, 20, 9,  SkillType.Woodcutting),

        // ── Water ────────────────────────────────────────────────────
        // Fishing's own ladder lives in SkillSeedData (Stage 07). The three placeholder
        // fish that sat here were removed rather than kept: they shared Fishing's tiers
        // and levels, so both ladders would have competed for the same tier band and the
        // "highest unlocked tier wins" rule would have picked between them arbitrarily.
        // Reeds survives as a non-Fishing water material so the terrain still has a pool
        // of its own.
        Tiered("reeds",         "Reeds",         MaterialCategory.Water, 1, 1,  3,  SkillType.Foraging),

        // ── Farmland (Farming) ───────────────────────────────────────
        Tiered("fibre",         "Plant Fibre",   MaterialCategory.Farmland, 1, 1,  3,  SkillType.Farming),
        Tiered("grain",         "Grain",         MaterialCategory.Farmland, 2, 10, 5,  SkillType.Farming),

        // ── Urban (Trading) ──────────────────────────────────────────
        Tiered("scrap",         "Scrap",         MaterialCategory.Urban, 1, 1,  3,  SkillType.Trading),
        Tiered("salvage",       "Salvage",       MaterialCategory.Urban, 2, 10, 5,  SkillType.Trading),

        // ── Industrial (Smithing) ────────────────────────────────────
        Tiered("slag",          "Slag",          MaterialCategory.Industrial, 1, 1,  3,  SkillType.Smithing),
        Tiered("ingot_crude",   "Crude Ingot",   MaterialCategory.Industrial, 2, 10, 5,  SkillType.Smithing),
        Tiered("coal",          "Coal",          MaterialCategory.Industrial, 3, 20, 9,  SkillType.Smithing),

        // ── Rocky (Mining) ───────────────────────────────────────────
        // The §4.1a worked example. A landlocked player reaches Gold Ore at Mining 50;
        // it simply takes them longer than someone living by a quarry.
        Tiered("stone_rough",   "Rough Stone",   MaterialCategory.Rocky, 1, 1,  3,  SkillType.Mining),
        Tiered("ore_copper",    "Copper Ore",    MaterialCategory.Rocky, 2, 10, 5,  SkillType.Mining),
        Tiered("ore_iron",      "Iron Ore",      MaterialCategory.Rocky, 3, 20, 9,  SkillType.Mining),
        Tiered("ore_silver",    "Silver Ore",    MaterialCategory.Rocky, 4, 35, 15, SkillType.Mining),
        Tiered("ore_gold",      "Gold Ore",      MaterialCategory.Rocky, 5, 50, 24, SkillType.Mining),

        // ── Coastal ──────────────────────────────────────────────────
        // Likewise reassigned to Foraging: beachcombing is foraging, and leaving them on
        // Fishing would collide with its Stage 07 ladder.
        Tiered("driftwood",     "Driftwood",     MaterialCategory.Coastal, 1, 1,  3,  SkillType.Foraging),
        Tiered("shellfish",     "Shellfish",     MaterialCategory.Coastal, 2, 10, 5,  SkillType.Foraging),

        // ── Unique named materials (§7.4: reserve for the rarest) ────
        // Three of a ≤15 budget. The rest arrive with the POI systems that justify them,
        // rather than being invented now to fill a quota.
        // Each sits in its own skill's ladder, so they do not compete for a tier slot
        // with each other. They are excluded from cell drop tables entirely.
        Tiered("blessed_water", "Blessed Water", MaterialCategory.Relic, 3, 20, 9,  SkillType.Prayer,    isUnique: true, stackCap: 50),
        Tiered("ancient_tome",  "Ancient Tome",  MaterialCategory.Relic, 4, 35, 15, SkillType.Knowledge, isUnique: true, stackCap: 50),
        Tiered("relic_shard",   "Relic Shard",   MaterialCategory.Relic, 5, 50, 24, SkillType.Combat,    isUnique: true, stackCap: 50),
    };

    /// <summary>
    /// Which material pools each terrain draws from.
    ///
    /// <see cref="TerrainType.Open"/> maps to the base pool so an unclassified cell still
    /// yields something — a cell that yields nothing is a geographic dead zone.
    /// </summary>
    public static IReadOnlyDictionary<TerrainType, MaterialCategory[]> TerrainPools { get; } =
        new Dictionary<TerrainType, MaterialCategory[]>
        {
            [TerrainType.Open] = [MaterialCategory.Dust, MaterialCategory.Urban],
            [TerrainType.Woodland] = [MaterialCategory.Woodland],
            [TerrainType.Water] = [MaterialCategory.Water],
            [TerrainType.Farmland] = [MaterialCategory.Farmland],
            [TerrainType.Urban] = [MaterialCategory.Urban],
            [TerrainType.Industrial] = [MaterialCategory.Industrial],
            [TerrainType.Rocky] = [MaterialCategory.Rocky],
            [TerrainType.Coastal] = [MaterialCategory.Coastal],
        };

    /// <summary>
    /// Drop entries for every terrain, derived from the pools rather than hand-listed so
    /// the two cannot drift apart.
    ///
    /// Unique materials are excluded: they come from POI visits (Stage 04+), not from
    /// walking over a cell, which is what keeps them rare.
    /// </summary>
    public static IEnumerable<(TerrainType Terrain, string MaterialKey, int Weight, int Min, int Max)> DropEntries()
    {
        foreach (var (terrain, categories) in TerrainPools)
        {
            foreach (var category in categories)
            {
                foreach (var material in Materials.Where(m => m.Category == category && !m.IsUnique))
                {
                    // Lower tiers are commoner within a terrain; the tier selection in
                    // DropRoller then prefers the highest unlocked band regardless.
                    var weight = Math.Max(1, 10 - (material.Tier - 1) * 2);

                    // Higher tiers yield fewer units per cell — the quantity is where
                    // BaseGatherSeconds shows up on a walk, since a walk cannot block
                    // on a timer (§4.1a).
                    var max = material.Tier <= 1 ? 3 : material.Tier <= 3 ? 2 : 1;

                    yield return (terrain, material.Key, weight, 1, max);
                }
            }
        }
    }
}
