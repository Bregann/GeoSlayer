using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Skills;

namespace GeoSlayer.Tests.Services.Skills;

/// <summary>
/// The tier rules, asserted for <b>every</b> gathering skill rather than one at a time.
///
/// <para>Stage 07 exists to prove a skill can be added as seed data alone. These tests are
/// the other half of that: they are parameterised over every seeded ladder, so adding
/// Woodcutting or Mining inherits the whole regression suite without a new test file. A
/// skill that breaks the geography rule fails here the moment it is seeded.</para>
/// </summary>
[TestFixture]
public class GatheringSkillLadderTests
{
    /// <summary>
    /// Every skill with a seeded tier ladder — <b>gathering and production alike</b>.
    ///
    /// The tier rules (levels, rates, XP/hour, absolute gating) apply to both. Only the
    /// terrain rules are gathering-specific, and those use
    /// <see cref="GatheringSkills"/> instead.
    /// </summary>
    private static IEnumerable<SkillType> LadderSkills =>
        SkillSeedData.AllSkillMaterials.Select(m => m.SkillType!.Value).Distinct();

    /// <summary>
    /// Skills trained by walking. Production skills (Cooking) train through the craft
    /// queue and deliberately have no terrain mapping, so the terrain rules do not apply
    /// to them — asserting otherwise would demand a mapping the design says must not
    /// exist.
    /// </summary>
    private static IEnumerable<SkillType> GatheringSkills =>
        LadderSkills.Where(s => !SkillSeedData.ProductionSkills.Contains(s));

    private static List<Material> Ladder(SkillType skill) =>
        SkillSeedData.AllSkillMaterials
            .Where(m => m.SkillType == skill)
            .OrderBy(m => m.Tier)
            .ToList();

    /// <summary>Build drop entries with ids assigned as a database would.</summary>
    private static (List<DropTableEntry> Entries, Dictionary<string, Material> ByKey) BuildTable()
    {
        var byKey = new Dictionary<string, Material>();
        var id = 1;

        foreach (var material in SkillSeedData.AllSkillMaterials)
        {
            byKey[material.Key] = new Material
            {
                Id = id++,
                Key = material.Key,
                Name = material.Name,
                Tier = material.Tier,
                Category = material.Category,
                SkillType = material.SkillType,
                StackCap = material.StackCap,
                IsUnique = material.IsUnique,
                LevelRequired = material.LevelRequired,
                BaseGatherSeconds = material.BaseGatherSeconds,
                XpPerUnit = material.XpPerUnit,
                DustPerOverflow = material.DustPerOverflow,
            };
        }

        var entries = new List<DropTableEntry>();
        var entryId = 1;
        var seen = new HashSet<(TerrainType, int)>();

        foreach (var (terrain, key, weight, min, max) in SkillSeedData.DropEntries())
        {
            if (!byKey.TryGetValue(key, out var material)) continue;
            if (!seen.Add((terrain, material.Id))) continue;

            entries.Add(new DropTableEntry
            {
                Id = entryId++,
                Terrain = terrain,
                MaterialId = material.Id,
                Material = material,
                Weight = weight,
                MinQuantity = min,
                MaxQuantity = max,
            });
        }

        return (entries, byKey);
    }

    // ── Ladder shape ────────────────────────────────────────────────

    [TestCaseSource(nameof(LadderSkills))]
    public void EverySkillHasSevenTiers_AtThePrescribedLevels(SkillType skill)
    {
        var levels = Ladder(skill).Select(m => m.LevelRequired).ToList();

        Assert.That(levels, Is.EqualTo(new[] { 1, 10, 20, 35, 50, 70, 90 }),
            $"{skill} does not follow the standard ladder");
    }

    [TestCaseSource(nameof(LadderSkills))]
    public void EverySkillMatchesTheTemplateRates(SkillType skill)
    {
        var expected = new[]
        {
            (3.0, 5.0), (5.0, 12.0), (9.0, 25.0), (15.0, 48.0),
            (24.0, 85.0), (40.0, 150.0), (60.0, 240.0),
        };

        var ladder = Ladder(skill);

        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Multiple(() =>
            {
                Assert.That(ladder[i].BaseGatherSeconds, Is.EqualTo(expected[i].Item1),
                    $"{skill} tier {i + 1} gather time");
                Assert.That(ladder[i].XpPerUnit, Is.EqualTo(expected[i].Item2),
                    $"{skill} tier {i + 1} XP");
            });
        }
    }

    // ── Criterion 7: XP/hour across tiers ───────────────────────────

    [TestCaseSource(nameof(LadderSkills))]
    public void XpPerHour_NeverFallsAsTiersRise(SkillType skill)
    {
        // Monotonic non-decreasing, the testable form of §4.1a's "never punished for
        // advancing". A dip would make staying on a lower tier optimal.
        var ladder = Ladder(skill);

        for (var i = 1; i < ladder.Count; i++)
        {
            var previous = ladder[i - 1].XpPerUnit / ladder[i - 1].BaseGatherSeconds;
            var current = ladder[i].XpPerUnit / ladder[i].BaseGatherSeconds;

            Assert.That(current, Is.GreaterThanOrEqualTo(previous),
                $"{skill} tier {ladder[i].Tier} pays less per second than tier {ladder[i - 1].Tier}");
        }
    }

    // ── Criterion 5: gating is absolute ─────────────────────────────

    [TestCaseSource(nameof(GatheringSkills))]
    public void EveryTier_IsUnreachableOneLevelBelowItsGate(SkillType skill)
    {
        var (entries, byKey) = BuildTable();

        foreach (var material in Ladder(skill).Where(m => m.LevelRequired > 1))
        {
            var levels = new Dictionary<SkillType, int> { [skill] = material.LevelRequired - 1 };
            var id = byKey[material.Key].Id;

            // Every terrain, so a generous one cannot smuggle the tier through.
            foreach (var terrain in new[]
                     {
                         TerrainType.Open, TerrainType.Water, TerrainType.Coastal,
                         TerrainType.Woodland, TerrainType.Farmland, TerrainType.Urban,
                     })
            {
                for (var cell = 0; cell < 120; cell++)
                {
                    var drops = DropRoller.Roll(
                        playerId: 1, gridLat: cell, gridLng: cell * 3,
                        terrain, entries, levels);

                    Assert.That(drops.Any(d => d.MaterialId == id), Is.False,
                        $"{material.Key} dropped at {skill} {material.LevelRequired - 1} on {terrain}");
                }
            }
        }
    }

    // ── Criterion 6: the geography-lockout regression test ──────────

    [TestCaseSource(nameof(GatheringSkills))]
    public void WithNoMatchingTerrain_EveryUnlockedTierIsStillReachable(SkillType skill)
    {
        var (entries, _) = BuildTable();

        // A level 90 player whose local terrain suits this skill least.
        var levels = new Dictionary<SkillType, int> { [skill] = 90 };

        // Find the skill's worst terrain from its own mappings — its base rate.
        var worst = SkillSeedData.TerrainMappings
            .Where(m => m.SkillType == skill && m.Terrain != TerrainType.Open)
            .OrderBy(m => m.XpPerCell)
            .Select(m => m.Terrain)
            .FirstOrDefault();

        if (worst == default) Assert.Ignore($"{skill} has no non-Open mappings");

        var reachable = entries
            .Where(e => e.Terrain == worst && e.Material.SkillType == skill)
            .Where(e => DropRoller.IsObtainable(e.Material, levels))
            .Select(e => e.Material.Key)
            .ToHashSet();

        foreach (var material in Ladder(skill))
        {
            Assert.That(reachable, Contains.Item(material.Key),
                $"{material.Key} is unreachable on {worst} — that is a geographic lockout");
        }
    }

    [TestCaseSource(nameof(GatheringSkills))]
    public void EveryGatheringSkillHasAnOpenTerrainMapping(SkillType skill)
    {
        // The Open row is the base rate, and the single thing standing between the design
        // and a geographic lockout. A gathering skill seeded without one silently trains
        // nothing on unclassified ground.
        var hasOpen = SkillSeedData.TerrainMappings
            .Any(m => m.SkillType == skill && m.Terrain == TerrainType.Open);

        Assert.That(hasOpen, Is.True,
            $"{skill} has no Open mapping — it would train nothing on unclassified ground");
    }

    [TestCaseSource(nameof(GatheringSkills))]
    public void EveryTerrain_TrainsEveryGatheringSkillSomething(SkillType skill)
    {
        var (entries, _) = BuildTable();
        var levels = new Dictionary<SkillType, int> { [skill] = 90 };

        foreach (var terrain in new[]
                 {
                     TerrainType.Open, TerrainType.Woodland, TerrainType.Water,
                     TerrainType.Farmland, TerrainType.Urban, TerrainType.Industrial,
                     TerrainType.Rocky, TerrainType.Coastal,
                 })
        {
            var drops = DropRoller.Roll(2, 500, 500, terrain, entries, levels);

            Assert.That(drops, Is.Not.Empty, $"{skill} yielded nothing on {terrain}");
        }
    }

    // ── Material keys stay unique across skills ─────────────────────

    [Test]
    public void MaterialKeysAreUniqueAcrossEverySkill()
    {
        // Two skills sharing a key would make one silently unseeded — the seeders insert
        // by key and skip duplicates.
        var keys = SkillSeedData.AllSkillMaterials.Select(m => m.Key).ToList();

        Assert.That(keys, Is.Unique);
    }

    [Test]
    public void NoSkillHasTwoMaterialsAtTheSameTier()
    {
        // The category test below only sees SkillSeedData. Stage 03 also seeded materials
        // against skills in MaterialSeedData, and two materials on one skill at one tier
        // compete for the same band — the roll picks between them arbitrarily, so one is
        // effectively invisible.
        var all = Domain.Services.Materials.MaterialSeedData.Materials
            .Concat(SkillSeedData.AllSkillMaterials)
            .Where(m => m.SkillType is not null);

        var clashes = all
            .GroupBy(m => (m.SkillType!.Value, m.Tier))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Item1} tier {g.Key.Tier}: {string.Join(", ", g.Select(m => m.Key))}")
            .ToList();

        Assert.That(clashes, Is.Empty,
            "two materials competing for one tier band:\n" + string.Join("\n", clashes));
    }

    [Test]
    public void SkillLaddersDoNotShareAMaterialCategory()
    {
        // Two ladders in one category compete for the same tier band, and the
        // "highest unlocked tier wins" rule would pick between them arbitrarily.
        var byCategory = SkillSeedData.AllSkillMaterials
            .GroupBy(m => m.Category)
            .Select(g => new { g.Key, Skills = g.Select(m => m.SkillType).Distinct().ToList() })
            .ToList();

        foreach (var group in byCategory)
        {
            Assert.That(group.Skills, Has.Count.EqualTo(1),
                $"category {group.Key} is shared by {string.Join(", ", group.Skills)}");
        }
    }

    // ── Production skills (Stage 09) ────────────────────────────────

    [Test]
    public void ProductionSkills_HaveNoTerrainMapping()
    {
        // The inverse of the gathering rule, and just as load-bearing: a production skill
        // with a terrain mapping would train by walking, which is not what it is for.
        foreach (var skill in SkillSeedData.ProductionSkills)
        {
            var mappings = SkillSeedData.TerrainMappings.Where(m => m.SkillType == skill);

            Assert.That(mappings, Is.Empty,
                $"{skill} is a production skill and must not train from terrain");
        }
    }

    [Test]
    public void ProductionMaterials_NeverAppearInDropTables()
    {
        // Cooked food is made, never found. A pie dropping out of a hedge would also
        // bypass the craft queue the skill exists to drive.
        var productionKeys = SkillSeedData.AllSkillMaterials
            .Where(m => m.SkillType is not null && SkillSeedData.ProductionSkills.Contains(m.SkillType.Value))
            .Select(m => m.Key)
            .ToHashSet();

        Assert.That(productionKeys, Is.Not.Empty, "fixture assumption: a production skill exists");

        var dropped = SkillSeedData.DropEntries()
            .Select(e => e.MaterialKey)
            .Where(productionKeys.Contains)
            .Distinct()
            .ToList();

        Assert.That(dropped, Is.Empty,
            $"production materials in drop tables: {string.Join(", ", dropped)}");
    }

    [TestCaseSource(nameof(LadderSkills))]
    public void EverySkillIncludingProduction_FollowsTheTierLadder(SkillType skill)
    {
        // The tier rules are universal — only the terrain rules are gathering-specific.
        var levels = Ladder(skill).Select(m => m.LevelRequired).ToList();

        Assert.That(levels, Is.EqualTo(new[] { 1, 10, 20, 35, 50, 70, 90 }));
    }
}
