using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Progression;

/// <summary>
/// The seeded unlock ladder (§3.1) and Bonus Point tree (§3.0a).
///
/// Held as code-defined rows that the seeder inserts if absent, rather than as literals
/// inside a migration: a migration that carries game data can never be re-run to retune it.
/// </summary>
public static class ProgressionSeedData
{
    /// <summary>
    /// The unlock ladder, transcribed from the §3.1 table.
    ///
    /// Level 1 grants Exploration <b>and</b> Foraging — the cold-start fix. A first walk
    /// that paints cells but drops nothing is a map-painting utility, not an RPG.
    /// Levels beyond 25 (the "30+" row) are left unseeded until those skills are playable.
    /// </summary>
    public static IReadOnlyList<UnlockDefinition> Ladder { get; } = new List<UnlockDefinition>
    {
        new() { AdventurerLevel = 1,  UnlockType = UnlockType.Skill,  Payload = nameof(SkillType.Exploration), DisplayName = "Exploration" },
        new() { AdventurerLevel = 1,  UnlockType = UnlockType.Skill,  Payload = nameof(SkillType.Foraging),    DisplayName = "Foraging" },
        new() { AdventurerLevel = 1,  UnlockType = UnlockType.System, Payload = "Inventory",                   DisplayName = "Materials & Inventory" },
        new() { AdventurerLevel = 1,  UnlockType = UnlockType.System, Payload = "Workers",                     DisplayName = "Workers" },

        new() { AdventurerLevel = 3,  UnlockType = UnlockType.Skill,  Payload = nameof(SkillType.Fishing),     DisplayName = "Fishing" },

        new() { AdventurerLevel = 5,  UnlockType = UnlockType.Skill,  Payload = nameof(SkillType.Woodcutting), DisplayName = "Woodcutting" },
        new() { AdventurerLevel = 5,  UnlockType = UnlockType.System, Payload = "Claims",                      DisplayName = "Claims" },

        new() { AdventurerLevel = 8,  UnlockType = UnlockType.Skill,  Payload = nameof(SkillType.Cooking),     DisplayName = "Cooking" },
        new() { AdventurerLevel = 8,  UnlockType = UnlockType.System, Payload = "Crafting",                    DisplayName = "Crafting" },

        new() { AdventurerLevel = 12, UnlockType = UnlockType.Skill,  Payload = nameof(SkillType.Mining),      DisplayName = "Mining" },

        new() { AdventurerLevel = 16, UnlockType = UnlockType.Skill,  Payload = nameof(SkillType.Smithing),    DisplayName = "Smithing" },
        new() { AdventurerLevel = 16, UnlockType = UnlockType.System, Payload = "Buildings",                   DisplayName = "Buildings" },

        new() { AdventurerLevel = 20, UnlockType = UnlockType.Skill,  Payload = nameof(SkillType.Farming),     DisplayName = "Farming" },

        new() { AdventurerLevel = 25, UnlockType = UnlockType.Skill,  Payload = nameof(SkillType.Trading),     DisplayName = "Trading" },
        new() { AdventurerLevel = 25, UnlockType = UnlockType.System, Payload = "Economy",                     DisplayName = "Economy & Market" },

        // ── The "30+" tier (Stage 15) ────────────────────────────────────────────────
        // §3.1's table groups these as one row; Stage 15 gives each its own level so the
        // roadmap keeps drip-feeding rather than dumping seven skills at once.
        new() { AdventurerLevel = 30, UnlockType = UnlockType.Skill,  Payload = nameof(SkillType.Prayer),      DisplayName = "Prayer" },
        new() { AdventurerLevel = 32, UnlockType = UnlockType.Skill,  Payload = nameof(SkillType.Knowledge),   DisplayName = "Knowledge" },
        new() { AdventurerLevel = 34, UnlockType = UnlockType.Skill,  Payload = nameof(SkillType.Healing),     DisplayName = "Healing" },
        new() { AdventurerLevel = 36, UnlockType = UnlockType.Skill,  Payload = nameof(SkillType.Athletics),   DisplayName = "Athletics" },
        new() { AdventurerLevel = 38, UnlockType = UnlockType.Skill,  Payload = nameof(SkillType.Tavern),      DisplayName = "Tavern" },
        new() { AdventurerLevel = 40, UnlockType = UnlockType.Skill,  Payload = nameof(SkillType.Banking),     DisplayName = "Banking" },
        new() { AdventurerLevel = 42, UnlockType = UnlockType.Skill,  Payload = nameof(SkillType.Combat),      DisplayName = "Combat" },
        new() { AdventurerLevel = 42, UnlockType = UnlockType.System, Payload = "PoiFeatures",                 DisplayName = "POI Features" },
    };

    /// <summary>
    /// The four Stage 02 upgrades. §3.0a's table is longer, but the stage ships a
    /// deliberately small tree — "four balanced beats fifteen unbalanced" — and the rest
    /// arrive with the systems they affect.
    ///
    /// Effects come straight from §3.0a. Cost curves are the tuning part: §3.0a gives
    /// "1, 2, 4, 7, 11" for Worker Slot explicitly and says the powerful ones escalate,
    /// so Reveal Radius (flagged "very strong; high cost, few ranks") escalates hardest
    /// and the incremental percentage upgrades stay cheap with many ranks.
    /// </summary>
    public static IReadOnlyList<UpgradeDefinition> Upgrades { get; } = new List<UpgradeDefinition>
    {
        new()
        {
            Key = ProgressionDefaults.UpgradeKeys.WorkerSlot,
            Name = "Extra Worker Slot",
            Category = "Workers",
            MaxRank = 5,
            CostCurve = "1,2,4,7,11",   // §3.0a gives this curve verbatim.
            EffectPerRank = 1,          // +1 worker
            MinAdventurerLevel = 1,
            Description = "+1 worker slot. Workers train skills slowly while you're away.",
        },
        new()
        {
            Key = ProgressionDefaults.UpgradeKeys.RevealRadius,
            Name = "Reveal Radius",
            Category = "Exploration",
            MaxRank = 3,
            CostCurve = "3,8,16",       // "Very strong; high cost, few ranks."
            EffectPerRank = 1,          // +1 cell of radius
            MinAdventurerLevel = 1,
            Description = "+1 cell reveal radius. Every step paints a wider corridor.",
        },
        new()
        {
            Key = ProgressionDefaults.UpgradeKeys.Scholar,
            Name = "Scholar",
            Category = "Yield",
            MaxRank = 5,
            CostCurve = "2,3,4,6,8",    // "Broad, incremental" — the compounding pick.
            EffectPerRank = 0.05,       // +5% skill XP
            MinAdventurerLevel = 1,
            Description = "+5% XP in every skill.",
        },
        new()
        {
            Key = ProgressionDefaults.UpgradeKeys.ClaimSlot,
            Name = "Claim Slot",
            Category = "Territory",
            MaxRank = 4,
            CostCurve = "2,4,7,11",     // §3.0a lists Claim Slot as escalating.
            EffectPerRank = 1,          // +1 Claim
            MinAdventurerLevel = 5,     // Claims themselves unlock at Adventurer 5.
            Description = "+1 territory Claim. Claims are where workers are stationed.",
        },
        new()
        {
            Key = ProgressionDefaults.UpgradeKeys.CraftSlot,
            Name = "Craft Slot",
            Category = "Crafting",
            MaxRank = 3,
            CostCurve = "3,6,10",
            EffectPerRank = 1,          // +1 concurrent craft
            MinAdventurerLevel = 8,     // Crafting itself unlocks at Adventurer 8.
            Description = "+1 craft running at once. Crafts finish while you are away.",
        },
        new()
        {
            Key = ProgressionDefaults.UpgradeKeys.OfflineCap,
            Name = "Offline Cap",
            Category = "Idle",
            MaxRank = 4,
            CostCurve = "2,4,7,11",     // "The headline idle upgrade."
            EffectPerRank = 2,          // +2h offline accrual
            MinAdventurerLevel = 3,     // Gated so the tree reveals itself gradually.
            Description = "+2 hours of offline worker accrual before the cap bites.",
        },
    };
}
