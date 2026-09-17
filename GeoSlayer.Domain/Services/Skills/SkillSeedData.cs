using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Skills
{
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
            new()
            {
                SkillType = SkillType.Fishing,
                Name = "Fishing",
                Description = "Working the water. Rivers and coastline are best — but a landlocked angler still lands every fish, just slower.",
                Icon = "🎣",
                UnlockLevel = 3,
                Category = SkillCategory.Gathering,
            },
            new()
            {
                SkillType = SkillType.Smithing,
                Name = "Smithing",
                Description = "Working ore into tools. Consumes what Mining produces tier for tier, and gives back the tools that make mining faster — the loop that makes both worth levelling.",
                Icon = "🔨",
                UnlockLevel = 16,
                Category = SkillCategory.Production,
            },
            new()
            {
                SkillType = SkillType.Mining,
                Name = "Mining",
                Description = "Working stone and ore. Quarries and industry are best — and this is the skill that proves geography never locks you out, because most players have neither.",
                Icon = "⛏️",
                UnlockLevel = 12,
                Category = SkillCategory.Gathering,
            },
            new()
            {
                SkillType = SkillType.Cooking,
                Name = "Cooking",
                Description = "Turning what you gathered into what your workers eat. Trained at the fire, not on the road — the first skill that consumes rather than collects.",
                Icon = "🍳",
                UnlockLevel = 8,
                Category = SkillCategory.Production,
            },
            new()
            {
                SkillType = SkillType.Woodcutting,
                Name = "Woodcutting",
                Description = "Felling and splitting. Woodland is best, and a woodland cell trains this alongside Foraging — the same ground, two different harvests.",
                Icon = "🪓",
                UnlockLevel = 5,
                Category = SkillCategory.Gathering,
            },
            new()
            {
                SkillType = SkillType.Farming,
                Name = "Farming",
                Description = "Working the land. Slow-burn and worker-heavy by design — the idle-flavoured skill.",
                Icon = "🌾",
                UnlockLevel = 20,
                Category = SkillCategory.Gathering,
            },
            new()
            {
                SkillType = SkillType.Trading,
                Name = "Trading",
                Description = "Buying low and selling on. Opens the economy: materials to coin, coin to upkeep.",
                Icon = "💰",
                UnlockLevel = 25,
                Category = SkillCategory.Social,
            },
            new()
            {
                SkillType = SkillType.Prayer,
                Name = "Prayer",
                Description = "Quiet places and old stone. A cathedral is worth more than a chapel, but both count.",
                Icon = "🙏",
                UnlockLevel = 30,
                Category = SkillCategory.Social,
            },
            new()
            {
                SkillType = SkillType.Knowledge,
                Name = "Knowledge",
                Description = "Libraries, museums and lecture halls. Pairs naturally with the Museum.",
                Icon = "📚",
                UnlockLevel = 32,
                Category = SkillCategory.Social,
            },
            new()
            {
                SkillType = SkillType.Healing,
                Name = "Healing",
                Description = "Mending what is broken. Trained where the ailing are looked after.",
                Icon = "❤️",
                UnlockLevel = 34,
                Category = SkillCategory.Social,
            },
            new()
            {
                SkillType = SkillType.Athletics,
                Name = "Athletics",
                Description = "Distance and effort. The skill that most directly rewards simply walking further.",
                Icon = "🏃",
                UnlockLevel = 36,
                Category = SkillCategory.Social,
            },
            new()
            {
                SkillType = SkillType.Tavern,
                Name = "Tavern",
                Description = "Pubs, cafés and somewhere to sit down. Dense in cities, thinner elsewhere.",
                Icon = "🍺",
                UnlockLevel = 38,
                Category = SkillCategory.Social,
            },
            new()
            {
                SkillType = SkillType.Banking,
                Name = "Banking",
                Description = "Coin and the keeping of it. Storage-flavoured.",
                Icon = "🏦",
                UnlockLevel = 40,
                Category = SkillCategory.Social,
            },
            new()
            {
                SkillType = SkillType.Combat,
                Name = "Combat",
                Description = "Castles, forts and battlefields. Treated as a gathering skill — historic ground is a boost, never the only venue.",
                Icon = "⚔️",
                UnlockLevel = 42,
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

            // ── Fishing (Stage 07) ────────────────────────────────────────────────────────
            // Water and coastline are best; everything else still trains at base rate. The
            // Open row is what guarantees a landlocked player is never locked out (§5.2).
            new() { SkillType = SkillType.Fishing, Terrain = TerrainType.Open,       XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Fishing, Terrain = TerrainType.Water,      XpPerCell = 3.0, YieldMultiplier = 2.0 },
            new() { SkillType = SkillType.Fishing, Terrain = TerrainType.Coastal,    XpPerCell = 3.0, YieldMultiplier = 2.0 },
            new() { SkillType = SkillType.Fishing, Terrain = TerrainType.Farmland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Fishing, Terrain = TerrainType.Woodland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Fishing, Terrain = TerrainType.Urban,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Fishing, Terrain = TerrainType.Industrial, XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Fishing, Terrain = TerrainType.Rocky,      XpPerCell = 1.0, YieldMultiplier = 1.0 },

            // ── Woodcutting (Stage 08) ────────────────────────────────────────────────────
            // Woodland is best. Note this overlaps Foraging deliberately: one woodland cell
            // trains both, and each rolls its own tier from its own ladder.
            new() { SkillType = SkillType.Woodcutting, Terrain = TerrainType.Open,       XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Woodcutting, Terrain = TerrainType.Woodland,   XpPerCell = 3.0, YieldMultiplier = 2.0 },
            new() { SkillType = SkillType.Woodcutting, Terrain = TerrainType.Farmland,   XpPerCell = 1.5, YieldMultiplier = 1.25 },
            new() { SkillType = SkillType.Woodcutting, Terrain = TerrainType.Coastal,    XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Woodcutting, Terrain = TerrainType.Water,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Woodcutting, Terrain = TerrainType.Urban,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Woodcutting, Terrain = TerrainType.Industrial, XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Woodcutting, Terrain = TerrainType.Rocky,      XpPerCell = 1.0, YieldMultiplier = 1.0 },

            // ── Mining (Stage 10) ─────────────────────────────────────────────────────────
            // The accessibility stress test. Rocky and industrial ground is genuinely rare
            // for most players, so the Open row here is doing the most work of any mapping
            // in the game: it is what lets a suburban player reach Meteoric Ore at all.
            new() { SkillType = SkillType.Mining, Terrain = TerrainType.Open,       XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Mining, Terrain = TerrainType.Rocky,      XpPerCell = 3.0, YieldMultiplier = 2.0 },
            new() { SkillType = SkillType.Mining, Terrain = TerrainType.Industrial, XpPerCell = 3.0, YieldMultiplier = 2.0 },
            new() { SkillType = SkillType.Mining, Terrain = TerrainType.Coastal,    XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Mining, Terrain = TerrainType.Water,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Mining, Terrain = TerrainType.Woodland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Mining, Terrain = TerrainType.Farmland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Mining, Terrain = TerrainType.Urban,      XpPerCell = 1.0, YieldMultiplier = 1.0 },

            // ── Farming (Stage 15) ──
            new() { SkillType = SkillType.Farming, Terrain = TerrainType.Open,       XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Farming, Terrain = TerrainType.Woodland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Farming, Terrain = TerrainType.Water,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Farming, Terrain = TerrainType.Farmland,   XpPerCell = 3.0, YieldMultiplier = 2.0 },
            new() { SkillType = SkillType.Farming, Terrain = TerrainType.Urban,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Farming, Terrain = TerrainType.Industrial, XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Farming, Terrain = TerrainType.Rocky,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Farming, Terrain = TerrainType.Coastal,    XpPerCell = 1.0, YieldMultiplier = 1.0 },
            // ── Trading (Stage 15) ──
            new() { SkillType = SkillType.Trading, Terrain = TerrainType.Open,       XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Trading, Terrain = TerrainType.Woodland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Trading, Terrain = TerrainType.Water,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Trading, Terrain = TerrainType.Farmland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Trading, Terrain = TerrainType.Urban,      XpPerCell = 3.0, YieldMultiplier = 2.0 },
            new() { SkillType = SkillType.Trading, Terrain = TerrainType.Industrial, XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Trading, Terrain = TerrainType.Rocky,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Trading, Terrain = TerrainType.Coastal,    XpPerCell = 1.0, YieldMultiplier = 1.0 },
            // ── Prayer (Stage 15) ──
            new() { SkillType = SkillType.Prayer, Terrain = TerrainType.Open,       XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Prayer, Terrain = TerrainType.Woodland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Prayer, Terrain = TerrainType.Water,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Prayer, Terrain = TerrainType.Farmland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Prayer, Terrain = TerrainType.Urban,      XpPerCell = 3.0, YieldMultiplier = 2.0 },
            new() { SkillType = SkillType.Prayer, Terrain = TerrainType.Industrial, XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Prayer, Terrain = TerrainType.Rocky,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Prayer, Terrain = TerrainType.Coastal,    XpPerCell = 1.0, YieldMultiplier = 1.0 },
            // ── Knowledge (Stage 15) ──
            new() { SkillType = SkillType.Knowledge, Terrain = TerrainType.Open,       XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Knowledge, Terrain = TerrainType.Woodland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Knowledge, Terrain = TerrainType.Water,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Knowledge, Terrain = TerrainType.Farmland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Knowledge, Terrain = TerrainType.Urban,      XpPerCell = 3.0, YieldMultiplier = 2.0 },
            new() { SkillType = SkillType.Knowledge, Terrain = TerrainType.Industrial, XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Knowledge, Terrain = TerrainType.Rocky,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Knowledge, Terrain = TerrainType.Coastal,    XpPerCell = 1.0, YieldMultiplier = 1.0 },
            // ── Healing (Stage 15) ──
            new() { SkillType = SkillType.Healing, Terrain = TerrainType.Open,       XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Healing, Terrain = TerrainType.Woodland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Healing, Terrain = TerrainType.Water,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Healing, Terrain = TerrainType.Farmland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Healing, Terrain = TerrainType.Urban,      XpPerCell = 3.0, YieldMultiplier = 2.0 },
            new() { SkillType = SkillType.Healing, Terrain = TerrainType.Industrial, XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Healing, Terrain = TerrainType.Rocky,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Healing, Terrain = TerrainType.Coastal,    XpPerCell = 1.0, YieldMultiplier = 1.0 },
            // ── Athletics (Stage 15) ──
            new() { SkillType = SkillType.Athletics, Terrain = TerrainType.Open,       XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Athletics, Terrain = TerrainType.Woodland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Athletics, Terrain = TerrainType.Water,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Athletics, Terrain = TerrainType.Farmland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Athletics, Terrain = TerrainType.Urban,      XpPerCell = 3.0, YieldMultiplier = 2.0 },
            new() { SkillType = SkillType.Athletics, Terrain = TerrainType.Industrial, XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Athletics, Terrain = TerrainType.Rocky,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Athletics, Terrain = TerrainType.Coastal,    XpPerCell = 1.0, YieldMultiplier = 1.0 },
            // ── Tavern (Stage 15) ──
            new() { SkillType = SkillType.Tavern, Terrain = TerrainType.Open,       XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Tavern, Terrain = TerrainType.Woodland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Tavern, Terrain = TerrainType.Water,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Tavern, Terrain = TerrainType.Farmland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Tavern, Terrain = TerrainType.Urban,      XpPerCell = 3.0, YieldMultiplier = 2.0 },
            new() { SkillType = SkillType.Tavern, Terrain = TerrainType.Industrial, XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Tavern, Terrain = TerrainType.Rocky,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Tavern, Terrain = TerrainType.Coastal,    XpPerCell = 1.0, YieldMultiplier = 1.0 },
            // ── Banking (Stage 15) ──
            new() { SkillType = SkillType.Banking, Terrain = TerrainType.Open,       XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Banking, Terrain = TerrainType.Woodland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Banking, Terrain = TerrainType.Water,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Banking, Terrain = TerrainType.Farmland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Banking, Terrain = TerrainType.Urban,      XpPerCell = 3.0, YieldMultiplier = 2.0 },
            new() { SkillType = SkillType.Banking, Terrain = TerrainType.Industrial, XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Banking, Terrain = TerrainType.Rocky,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Banking, Terrain = TerrainType.Coastal,    XpPerCell = 1.0, YieldMultiplier = 1.0 },
            // ── Combat (Stage 15) ──
            new() { SkillType = SkillType.Combat, Terrain = TerrainType.Open,       XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Combat, Terrain = TerrainType.Woodland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Combat, Terrain = TerrainType.Water,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Combat, Terrain = TerrainType.Farmland,   XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Combat, Terrain = TerrainType.Urban,      XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Combat, Terrain = TerrainType.Industrial, XpPerCell = 1.0, YieldMultiplier = 1.0 },
            new() { SkillType = SkillType.Combat, Terrain = TerrainType.Rocky,      XpPerCell = 3.0, YieldMultiplier = 2.0 },
            new() { SkillType = SkillType.Combat, Terrain = TerrainType.Coastal,    XpPerCell = 1.0, YieldMultiplier = 1.0 },
        };

        /// <summary>
        /// Builds a skill's seven-tier ladder from its material names.
        ///
        /// <para>Shared by every gathering skill: the tier shape (levels, gather seconds,
        /// XP) comes from <see cref="StandardLadder"/>, so a new skill supplies only names.
        /// This is what makes adding a skill seed data rather than code.</para>
        /// </summary>
        public static List<Material> BuildLadder(
            SkillType skill,
            MaterialCategory category,
            (string Key, string Name)[] tiers)
        {
            if (tiers.Length != StandardLadder.Length)
            {
                throw new ArgumentException(
                    $"{skill} supplied {tiers.Length} tiers; the standard ladder has {StandardLadder.Length}.",
                    nameof(tiers));
            }

            var materials = new List<Material>();

            foreach (var (tier, level, seconds, xp) in StandardLadder)
            {
                var (key, name) = tiers[tier - 1];

                materials.Add(new Material
                {
                    Key = key,
                    Name = name,
                    Category = category,
                    Tier = tier,
                    SkillType = skill,
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

        /// <summary>Foraging's ladder — the Stage 04 reference every later skill copies.</summary>
        public static IReadOnlyList<Material> ForagingMaterials { get; } = BuildLadder(
            SkillType.Foraging,
            MaterialCategory.Foraged,
            [
                ("wild_grass",      "Wild Grass"),
                ("common_herbs",    "Common Herbs"),
                ("berries",         "Berries"),
                ("root_vegetables", "Root Vegetables"),
                ("rare_fungi",      "Rare Fungi"),
                ("nightbloom",      "Nightbloom"),
                ("everleaf",        "Everleaf"),
            ]);

        /// <summary>
        /// Fishing's ladder (Stage 07). Added as pure seed data — no C# beyond this list,
        /// which is what Stage 07 exists to prove about the Stage 04 machinery.
        /// </summary>
        public static IReadOnlyList<Material> FishingMaterials { get; } = BuildLadder(
            SkillType.Fishing,
            MaterialCategory.Caught,
            [
                ("minnow",    "Minnow"),
                ("sardine",   "Sardine"),
                ("trout",     "Trout"),
                ("salmon",    "Salmon"),
                ("pike",      "Pike"),
                ("sturgeon",  "Sturgeon"),
                ("moonfish",  "Moonfish"),
            ]);

        /// <summary>Woodcutting's ladder (Stage 08).</summary>
        public static IReadOnlyList<Material> WoodcuttingMaterials { get; } = BuildLadder(
            SkillType.Woodcutting,
            MaterialCategory.Logged,
            [
                ("deadwood",  "Deadwood"),
                ("softwood",  "Softwood"),
                ("oak",       "Oak"),
                ("ash",       "Ash"),
                ("yew",       "Yew"),
                ("ironbark",  "Ironbark"),
                ("elderwood", "Elderwood"),
            ]);

        /// <summary>
        /// Cooking's ladder (Stage 09) — the first <b>production</b> skill.
        ///
        /// <para>Deliberately has <b>no terrain mapping</b>: production skills train through
        /// the craft queue, not by walking. That is a different code path from gathering, and
        /// the reason these materials are excluded from <see cref="DropEntries"/> — cooked
        /// food is made, never found on the ground.</para>
        /// </summary>
        public static IReadOnlyList<Material> CookingMaterials { get; } = BuildLadder(
            SkillType.Cooking,
            MaterialCategory.Cooked,
            [
                ("dried_rations",      "Dried Rations"),
                ("travellers_stew",    "Traveller's Stew"),
                ("hearty_pie",         "Hearty Pie"),
                ("spiced_roast",       "Spiced Roast"),
                ("feast_platter",      "Feast Platter"),
                ("preserved_banquet",  "Preserved Banquet"),
                ("ambrosia",           "Ambrosia"),
            ]);

        /// <summary>Mining's ladder (Stage 10) — the §4.1a worked example.</summary>
        public static IReadOnlyList<Material> MiningMaterials { get; } = BuildLadder(
            SkillType.Mining,
            MaterialCategory.Mined,
            [
                // Keys carried over from Stage 03, which sketched the first five of these as
                // placeholders. Adopting the keys rather than inventing new ones keeps the
                // existing Stage 03 tests meaningful and avoids two names for one ore.
                ("stone_rough",  "Rough Stone"),
                ("ore_copper",   "Copper Ore"),
                ("ore_iron",     "Iron Ore"),
                ("ore_silver",   "Silver Ore"),
                ("ore_gold",     "Gold Ore"),
                ("gemstone",     "Gemstone"),
                ("meteoric_ore", "Meteoric Ore"),
            ]);

        /// <summary>Smithing's ladder (Stage 11) — a production skill, like Cooking.</summary>
        public static IReadOnlyList<Material> SmithingMaterials { get; } = BuildLadder(
            SkillType.Smithing,
            MaterialCategory.Forged,
            [
                ("stone_tools",       "Stone Tools"),
                ("copper_pickaxe",    "Copper Pickaxe"),
                ("iron_pickaxe",      "Iron Pickaxe"),
                ("silver_tools",      "Silver Tools"),
                ("gold_instruments",  "Gold Instruments"),
                ("gemset_tools",      "Gem-set Tools"),
                ("meteoric_gear",     "Meteoric Gear"),
            ]);

        /// <summary>Farming's ladder (Stage 15).</summary>
        public static IReadOnlyList<Material> FarmingMaterials { get; } = BuildLadder(
            SkillType.Farming, MaterialCategory.Grown,
            [
                ("chaff", "Chaff"), ("field_grain", "Grain"), ("root_crop", "Root Crop"),
                ("orchard_fruit", "Orchard Fruit"), ("prize_livestock", "Prize Livestock"),
                ("heirloom_seed", "Heirloom Seed"), ("goldenwheat", "Goldenwheat"),
            ]);

        /// <summary>Trading's ladder (Stage 15).</summary>
        public static IReadOnlyList<Material> TradingMaterials { get; } = BuildLadder(
            SkillType.Trading, MaterialCategory.Traded,
            [
                ("trinket", "Trinket"), ("textile", "Textile"), ("spice", "Spice"),
                ("fine_cloth", "Fine Cloth"), ("jewellery", "Jewellery"),
                ("artefact", "Artefact"), ("royal_commission", "Royal Commission"),
            ]);

        /// <summary>Prayer's ladder (Stage 15).</summary>
        public static IReadOnlyList<Material> PrayerMaterials { get; } = BuildLadder(
            SkillType.Prayer, MaterialCategory.Sacred,
            [
                ("tallow_candle", "Tallow Candle"), ("incense", "Incense"),
                ("consecrated_water", "Blessed Water"), ("relic_fragment", "Relic Shard"),
                ("sacred_text", "Sacred Text"), ("reliquary", "Reliquary"),
                ("saints_token", "Saint's Token"),
            ]);

        /// <summary>Knowledge's ladder (Stage 15).</summary>
        public static IReadOnlyList<Material> KnowledgeMaterials { get; } = BuildLadder(
            SkillType.Knowledge, MaterialCategory.Written,
            [
                ("scrap_note", "Scrap Note"), ("ink", "Ink"), ("parchment", "Parchment"),
                ("bound_tome", "Bound Tome"), ("rare_manuscript", "Rare Manuscript"),
                ("star_chart", "Star Chart"), ("lost_codex", "Lost Codex"),
            ]);

        /// <summary>Healing's ladder (Stage 15).</summary>
        public static IReadOnlyList<Material> HealingMaterials { get; } = BuildLadder(
            SkillType.Healing, MaterialCategory.Remedy,
            [
                ("clean_water", "Clean Water"), ("bandage", "Bandage"), ("salve", "Salve"),
                ("tincture", "Tincture"), ("antidote", "Antidote"),
                ("panacea", "Panacea"), ("elixir_of_life", "Elixir of Life"),
            ]);

        /// <summary>Athletics' ladder (Stage 15).</summary>
        public static IReadOnlyList<Material> AthleticsMaterials { get; } = BuildLadder(
            SkillType.Athletics, MaterialCategory.Athletic,
            [
                ("worn_laces", "Worn Laces"), ("chalk", "Chalk"), ("resin", "Resin"),
                ("training_weights", "Training Weights"), ("endurance_draught", "Endurance Draught"),
                ("champions_sash", "Champion's Sash"), ("victors_laurel", "Victor's Laurel"),
            ]);

        /// <summary>Tavern's ladder (Stage 15).</summary>
        public static IReadOnlyList<Material> TavernMaterials { get; } = BuildLadder(
            SkillType.Tavern, MaterialCategory.Brewed,
            [
                ("small_ale", "Small Ale"), ("cider", "Cider"), ("stout", "Stout"),
                ("aged_wine", "Aged Wine"), ("spirits", "Spirits"),
                ("vintage_reserve", "Vintage Reserve"), ("legendary_cask", "Legendary Cask"),
            ]);

        /// <summary>Banking's ladder (Stage 15).</summary>
        public static IReadOnlyList<Material> BankingMaterials { get; } = BuildLadder(
            SkillType.Banking, MaterialCategory.Coin,
            [
                ("copper_coin", "Copper Coin"), ("silver_coin", "Silver Coin"),
                ("gold_coin", "Gold Coin"), ("promissory_note", "Promissory Note"),
                ("deed", "Deed"), ("bearer_bond", "Bearer Bond"), ("royal_charter", "Royal Charter"),
            ]);

        /// <summary>
        /// Combat's ladder (Stage 15).
        ///
        /// <para>Treated as a <b>gathering</b> skill per the stage's default: visit historic
        /// POIs, gain XP and relics. §9's open question about a full encounter system is left
        /// for a human — and if one is built, historic ground should be a <i>boost</i> rather
        /// than the only venue, since most players have no castle nearby.</para>
        /// </summary>
        public static IReadOnlyList<Material> CombatMaterials { get; } = BuildLadder(
            SkillType.Combat, MaterialCategory.Martial,
            [
                ("rusted_fragment", "Rusted Fragment"), ("iron_shard", "Iron Shard"),
                ("steel_fitting", "Steel Fitting"), ("officers_insignia", "Officer's Insignia"),
                ("warlords_seal", "Warlord's Seal"), ("ancient_blade", "Ancient Blade"),
                ("kings_relic", "King's Relic"),
            ]);

        /// <summary>
        /// Skills whose materials are <b>produced, not gathered</b>, so they are excluded
        /// from terrain drop tables. A cooked pie must not be found lying in a field.
        /// </summary>
        public static IReadOnlySet<SkillType> ProductionSkills { get; } =
            Definitions
                .Where(d => d.Category == SkillCategory.Production)
                .Select(d => d.SkillType)
                .ToHashSet();

        /// <summary>Every gathering skill's ladder, so seeders iterate rather than enumerate.</summary>
        public static IReadOnlyList<Material> AllSkillMaterials { get; } =
            [.. ForagingMaterials, .. FishingMaterials, .. WoodcuttingMaterials,
             .. CookingMaterials, .. MiningMaterials, .. SmithingMaterials,
             .. FarmingMaterials, .. TradingMaterials, .. PrayerMaterials,
             .. KnowledgeMaterials, .. HealingMaterials, .. AthleticsMaterials,
             .. TavernMaterials, .. BankingMaterials, .. CombatMaterials];

        /// <summary>
        /// Drop entries for the Foraging ladder, by terrain.
        ///
        /// Derived from <see cref="TerrainMappings"/> rather than listed separately, so a
        /// terrain added above cannot be forgotten here.
        /// </summary>
        public static IEnumerable<(TerrainType Terrain, string MaterialKey, int Weight, int Min, int Max)> DropEntries()
        {
            // Driven off the mappings and ladders rather than listed per skill, so adding a
            // skill above is genuinely all that adding a skill requires.
            foreach (var group in AllSkillMaterials.GroupBy(m => m.SkillType))
            {
                // Production materials are crafted, never found. Excluding them here is what
                // keeps a cooked pie from dropping out of a hedge.
                if (group.Key is not null && ProductionSkills.Contains(group.Key.Value))
                {
                    continue;
                }

                var terrains = TerrainMappings
                    .Where(m => m.SkillType == group.Key)
                    .Select(m => m.Terrain)
                    .Distinct()
                    .ToList();

                foreach (var terrain in terrains)
                {
                    foreach (var material in group)
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
    }
}
