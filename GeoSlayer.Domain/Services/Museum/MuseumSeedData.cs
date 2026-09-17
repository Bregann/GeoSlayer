using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Crafting;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Skills;

namespace GeoSlayer.Domain.Services.Museum;

/// <summary>
/// The Museum's plinths (DESIGN.md §5A.2), <b>derived</b> rather than authored.
///
/// <para>§5A.2's claim is that "you already have everything needed to populate this; the
/// entries fall out of existing data". This holds it to that: every wing is generated
/// from a table that already exists, so adding a skill or a POI tag adds its plinths for
/// free and the two can never drift apart.</para>
/// </summary>
public static class MuseumSeedData
{
    /// <summary>Key prefixes, so acquisition sites can build the same key a seeder did.</summary>
    public static class Keys
    {
        public const string Landmark = "landmark:";
        public const string Terrain = "terrain:";
        public const string Material = "material:";
        public const string Item = "item:";
        public const string Region = "region:";
        public const string Feat = "feat:";
    }

    public static string LandmarkKey(SkillType skill) => $"{Keys.Landmark}{skill}";
    public static string TerrainKey(TerrainType terrain) => $"{Keys.Terrain}{terrain}";
    public static string MaterialKey(string materialKey) => $"{Keys.Material}{materialKey}";
    public static string ItemKey(string itemKey) => $"{Keys.Item}{itemKey}";
    public static string FeatKey(string feat) => $"{Keys.Feat}{feat}";

    /// <summary>
    /// A Feat: a milestone derived from counters the game already keeps (§5A.2).
    /// </summary>
    public readonly record struct Feat(string Key, string Name, string Description, string Condition, int Threshold);

    /// <summary>
    /// Feats, derived from existing counters rather than new tracking.
    ///
    /// <para>Each threshold maps to something already recorded — revealed cells, a skill
    /// level, POI visits — so nothing here needs a new column.</para>
    /// </summary>
    public static IReadOnlyList<Feat> Feats { get; } =
    [
        new("cells_100",   "First Hundred",     "One hundred cells revealed.",              "RevealedCells", 100),
        new("cells_1000",  "Cartographer",      "A thousand cells revealed.",               "RevealedCells", 1_000),
        new("cells_10000", "Surveyor General",  "Ten thousand cells revealed.",             "RevealedCells", 10_000),

        new("pois_10",     "Sightseer",         "Ten different places visited.",            "PoisVisited", 10),
        new("pois_50",     "Well Travelled",    "Fifty different places visited.",          "PoisVisited", 50),
        new("pois_250",    "Inveterate Wanderer", "Two hundred and fifty places visited.",  "PoisVisited", 250),

        new("skill_25",    "Journeyman",        "Any skill to level 25.",                   "AnySkillLevel", 25),
        new("skill_50",    "Expert",            "Any skill to level 50.",                   "AnySkillLevel", 50),
        new("skill_90",    "Master",            "Any skill to level 90.",                   "AnySkillLevel", 90),

        new("adv_10",      "Making a Name",     "Adventurer level 10.",                     "AdventurerLevel", 10),
        new("adv_25",      "Seasoned",          "Adventurer level 25.",                     "AdventurerLevel", 25),
        new("adv_50",      "Renowned",          "Adventurer level 50.",                     "AdventurerLevel", 50),

        new("claims_1",    "Landholder",        "Your first Claim.",                        "Claims", 1),
        new("claims_10",   "Landed",            "Ten Claims held.",                         "Claims", 10),

        new("crafts_10",   "Apprentice Hand",   "Ten crafts completed.",                    "CraftsCompleted", 10),
        new("crafts_100",  "Practised Hand",    "One hundred crafts completed.",            "CraftsCompleted", 100),
    ];

    /// <summary>
    /// Every plinth, derived from the live tables.
    /// </summary>
    public static IEnumerable<MuseumEntryDefinition> Definitions()
    {
        var order = 0;

        // ── Landmarks: the ~80-entry OSM tag table, straight through (§5A.2) ──────────
        // One plinth per POI *type*, which is what the skill mapping already expresses.
        foreach (var skill in Enum.GetValues<SkillType>().OrderBy(s => s.ToString()))
        {
            yield return new MuseumEntryDefinition
            {
                Key = LandmarkKey(skill),
                Wing = MuseumWing.Landmarks,
                Name = $"{skill} Site",
                Description = $"You stood somewhere that teaches {skill}.",
                Rarity = MuseumRarity.Common,
                UnlockCondition = $"Visit any {skill} point of interest",
                SortOrder = order++,
            };
        }

        // ── Naturalist: terrain traversed ─────────────────────────────────────────────
        foreach (var terrain in Enum.GetValues<TerrainType>().Where(t => t != TerrainType.Open))
        {
            yield return new MuseumEntryDefinition
            {
                Key = TerrainKey(terrain),
                Wing = MuseumWing.Naturalist,
                Name = $"{terrain}",
                Description = $"You crossed {terrain.ToString().ToLowerInvariant()} ground.",
                Rarity = MuseumRarity.Common,
                UnlockCondition = $"Reveal a cell classified as {terrain}",
                SortOrder = order++,
            };
        }

        // ── Skill wings: every material, at every tier ────────────────────────────────
        var materials = MaterialSeedData.Materials
            .Concat(SkillSeedData.AllSkillMaterials)
            .Where(m => m.Category != MaterialCategory.Dust);

        foreach (var material in materials)
        {
            yield return new MuseumEntryDefinition
            {
                Key = MaterialKey(material.Key),
                Wing = material.IsUnique ? MuseumWing.Rarities : MuseumWing.Skills,
                Name = material.Name,
                Description = material.SkillType is null
                    ? $"Tier {material.Tier}."
                    : $"Tier {material.Tier} {material.SkillType}.",
                // Unique materials come only from rare POIs, so they are the genuinely
                // hard-to-find ones; tier stands in for rarity elsewhere.
                Rarity = material.IsUnique ? MuseumRarity.Legendary
                    : material.Tier >= 6 ? MuseumRarity.Rare
                    : material.Tier >= 4 ? MuseumRarity.Uncommon
                    : MuseumRarity.Common,
                UnlockCondition = material.IsUnique
                    ? "Find one at a rare place"
                    : $"Gather {material.Name}",
                SortOrder = order++,
            };
        }

        // ── Crafted items ─────────────────────────────────────────────────────────────
        foreach (var item in RecipeSeedData.Items)
        {
            yield return new MuseumEntryDefinition
            {
                Key = ItemKey(item.Key),
                Wing = MuseumWing.Skills,
                Name = item.Name,
                Description = item.Description,
                Rarity = item.Tier >= 5 ? MuseumRarity.Rare
                    : item.Tier >= 3 ? MuseumRarity.Uncommon
                    : MuseumRarity.Common,
                UnlockCondition = $"Craft {item.Name}",
                SortOrder = order++,
            };
        }

        // ── Feats ─────────────────────────────────────────────────────────────────────
        foreach (var feat in Feats)
        {
            yield return new MuseumEntryDefinition
            {
                Key = FeatKey(feat.Key),
                Wing = MuseumWing.Feats,
                Name = feat.Name,
                Description = feat.Description,
                Rarity = MuseumRarity.Uncommon,
                UnlockCondition = feat.Description,
                SortOrder = order++,
            };
        }

        // Cartography plinths are created on discovery rather than seeded — the set of
        // regions is unbounded and player-specific, so seeding every possible one is not
        // meaningful. Relics (Stage 13) and Expeditions (Stage 14) are defined as wings
        // but deliberately have no entries yet.
    }
}
