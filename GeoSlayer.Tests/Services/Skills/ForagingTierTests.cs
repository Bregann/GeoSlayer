using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Skills;

namespace GeoSlayer.Tests.Services.Skills;

/// <summary>
/// Stage 04 criteria 9–13 — the Foraging tier ladder and the no-special-casing rule.
///
/// Pure, so the gating rules can be asserted over thousands of rolls rather than sampled.
/// </summary>
[TestFixture]
public class ForagingTierTests
{
    /// <summary>Build Foraging drop entries with ids assigned as a database would.</summary>
    private static (List<DropTableEntry> Entries, Dictionary<string, Material> ByKey) BuildTable()
    {
        var byKey = new Dictionary<string, Material>();
        var id = 1;

        foreach (var material in SkillSeedData.ForagingMaterials)
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

    // ── The ladder shape ────────────────────────────────────────────

    [Test]
    public void ForagingHasSevenTiers_AtThePrescribedLevels()
    {
        var levels = SkillSeedData.ForagingMaterials
            .OrderBy(m => m.Tier)
            .Select(m => m.LevelRequired)
            .ToList();

        Assert.That(levels, Is.EqualTo(new[] { 1, 10, 20, 35, 50, 70, 90 }));
    }

    [Test]
    public void TheLadderMatchesTheTemplateExactly()
    {
        // SKILL-TEMPLATE.md §1a prescribes these. Every later skill copies them, so a
        // drift here would silently propagate through the whole game.
        var expected = new[]
        {
            (1, 3.0, 5.0), (2, 5.0, 12.0), (3, 9.0, 25.0), (4, 15.0, 48.0),
            (5, 24.0, 85.0), (6, 40.0, 150.0), (7, 60.0, 240.0),
        };

        foreach (var (tier, seconds, xp) in expected)
        {
            var material = SkillSeedData.ForagingMaterials.Single(m => m.Tier == tier);

            Assert.Multiple(() =>
            {
                Assert.That(material.BaseGatherSeconds, Is.EqualTo(seconds), $"tier {tier} gather time");
                Assert.That(material.XpPerUnit, Is.EqualTo(xp), $"tier {tier} XP");
            });
        }
    }

    // ── Criterion 12: XP/hour across tiers ──────────────────────────

    [Test]
    public void XpPerHour_NeverFallsAsTiersRise()
    {
        // The prescribed ladder is not flat — it climbs 6,000 to 14,400 XP/hour. That
        // still satisfies §4.1a's actual requirement, which is that a player is never
        // *punished* for advancing. Monotonic-non-decreasing is the testable form of
        // that; a dip at any tier would make staying on a lower tier optimal.
        var ordered = SkillSeedData.ForagingMaterials.OrderBy(m => m.Tier).ToList();

        for (var i = 1; i < ordered.Count; i++)
        {
            var previous = ordered[i - 1].XpPerUnit / ordered[i - 1].BaseGatherSeconds;
            var current = ordered[i].XpPerUnit / ordered[i].BaseGatherSeconds;

            Assert.That(current, Is.GreaterThanOrEqualTo(previous),
                $"tier {ordered[i].Tier} pays less per second than tier {ordered[i - 1].Tier}");
        }
    }

    [Test]
    public void HigherTiers_TakeLongerAndPayMorePerUnit()
    {
        var ordered = SkillSeedData.ForagingMaterials.OrderBy(m => m.Tier).ToList();

        for (var i = 1; i < ordered.Count; i++)
        {
            Assert.Multiple(() =>
            {
                Assert.That(ordered[i].BaseGatherSeconds, Is.GreaterThan(ordered[i - 1].BaseGatherSeconds));
                Assert.That(ordered[i].XpPerUnit, Is.GreaterThan(ordered[i - 1].XpPerUnit));
                Assert.That(ordered[i].LevelRequired, Is.GreaterThan(ordered[i - 1].LevelRequired));
            });
        }
    }

    // ── Criterion 9: gating is absolute ─────────────────────────────

    [Test]
    public void ALevel19Player_NeverReceivesBerries()
    {
        var (entries, byKey) = BuildTable();

        var berries = byKey["berries"];
        Assert.That(berries.LevelRequired, Is.EqualTo(20), "fixture assumption");

        var levels = new Dictionary<SkillType, int> { [SkillType.Foraging] = 19 };

        for (var cell = 0; cell < 2_000; cell++)
        {
            var drops = DropRoller.Roll(
                playerId: 1, gridLat: cell, gridLng: cell * 7,
                TerrainType.Woodland, entries, levels);

            Assert.That(drops.Any(d => d.MaterialId == berries.Id), Is.False,
                $"Berries dropped at Foraging 19 on cell {cell}");
        }
    }

    [Test]
    public void EveryTier_IsUnreachableOneLevelBelowItsGate()
    {
        var (entries, byKey) = BuildTable();

        foreach (var material in SkillSeedData.ForagingMaterials.Where(m => m.LevelRequired > 1))
        {
            var levels = new Dictionary<SkillType, int>
            {
                [SkillType.Foraging] = material.LevelRequired - 1,
            };

            var id = byKey[material.Key].Id;

            for (var cell = 0; cell < 300; cell++)
            {
                var drops = DropRoller.Roll(
                    playerId: 2, gridLat: cell, gridLng: cell * 3,
                    TerrainType.Woodland, entries, levels);

                Assert.That(drops.Any(d => d.MaterialId == id), Is.False,
                    $"{material.Key} dropped at level {material.LevelRequired - 1}");
            }
        }
    }

    // ── Criterion 10: unlocking makes it obtainable and favoured ────

    [Test]
    public void AtLevel20_BerriesBecomeObtainable()
    {
        var (entries, byKey) = BuildTable();
        var berries = byKey["berries"];

        var levels = new Dictionary<SkillType, int> { [SkillType.Foraging] = 20 };

        var found = false;

        for (var cell = 0; cell < 500 && !found; cell++)
        {
            var drops = DropRoller.Roll(
                playerId: 3, gridLat: cell, gridLng: cell * 5,
                TerrainType.Woodland, entries, levels);

            found = drops.Any(d => d.MaterialId == berries.Id);
        }

        Assert.That(found, Is.True, "Berries should be obtainable at Foraging 20");
    }

    [Test]
    public void TheTopUnlockedTier_OutweighsLowerOnes()
    {
        var (entries, byKey) = BuildTable();

        var levels = new Dictionary<SkillType, int> { [SkillType.Foraging] = 20 };

        var counts = new Dictionary<int, int>();

        for (var cell = 0; cell < 3_000; cell++)
        {
            var drops = DropRoller.Roll(
                playerId: 4, gridLat: cell, gridLng: cell * 11,
                TerrainType.Woodland, entries, levels);

            foreach (var drop in drops)
                counts[drop.MaterialId] = counts.GetValueOrDefault(drop.MaterialId) + 1;
        }

        var berries = counts.GetValueOrDefault(byKey["berries"].Id);
        var grass = counts.GetValueOrDefault(byKey["wild_grass"].Id);

        Assert.Multiple(() =>
        {
            Assert.That(berries, Is.GreaterThan(0), "the top unlocked tier should appear");
            Assert.That(grass, Is.Zero,
                "tier 1 falls outside the top-two fallback band once tier 3 is unlocked");
        });
    }

    // ── Criterion 11: the geography-lockout regression test ─────────

    [Test]
    public void WithNoWoodland_EveryUnlockedTierIsStillReachable()
    {
        var (entries, byKey) = BuildTable();

        // A level 90 player who lives somewhere with no woodland at all.
        var levels = new Dictionary<SkillType, int> { [SkillType.Foraging] = 90 };

        // Urban is the worst Foraging terrain, at base rate. Everleaf must still be
        // reachable there — tier gates on level, terrain gates only on speed (§4.1a).
        var reachable = entries
            .Where(e => e.Terrain == TerrainType.Urban)
            .Where(e => DropRoller.IsObtainable(e.Material, levels))
            .Select(e => e.Material.Key)
            .ToHashSet();

        foreach (var material in SkillSeedData.ForagingMaterials)
        {
            Assert.That(reachable, Contains.Item(material.Key),
                $"{material.Key} is unreachable without woodland — that is a geographic lockout");
        }
    }

    [Test]
    public void UrbanYieldsLessThanWoodland_ButNotNothing()
    {
        var (entries, _) = BuildTable();
        var levels = new Dictionary<SkillType, int> { [SkillType.Foraging] = 20 };

        var urban = 0;
        var woodland = 0;

        for (var cell = 0; cell < 500; cell++)
        {
            urban += DropRoller
                .Roll(5, cell, cell, TerrainType.Urban, entries, levels, terrainMultiplier: 1.0)
                .Sum(d => d.Quantity);

            woodland += DropRoller
                .Roll(5, cell, cell, TerrainType.Woodland, entries, levels, terrainMultiplier: 2.0)
                .Sum(d => d.Quantity);
        }

        Assert.Multiple(() =>
        {
            Assert.That(urban, Is.GreaterThan(0), "urban must still yield — terrain never gates");
            Assert.That(woodland, Is.GreaterThan(urban), "but woodland should be meaningfully better");
        });
    }

    // ── Criterion 13: no per-skill branches ─────────────────────────

    [Test]
    public void NoServiceCode_BranchesOnASpecificSkill()
    {
        // Adding a second skill must require only seed data. A `skill == SkillType.X`
        // branch in a service is the thing that gets copy-pasted eleven more times.
        var root = FindRepositoryRoot();
        var servicesDir = Path.Combine(root, "GeoSlayer.Domain", "Services");

        Assert.That(Directory.Exists(servicesDir), Is.True, $"expected services at {servicesDir}");

        var offenders = new List<string>();

        foreach (var file in Directory.GetFiles(servicesDir, "*.cs", SearchOption.AllDirectories))
        {
            // Seed data is *supposed* to name skills — that is the point of it.
            if (file.Contains("SeedData")) continue;

            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Comments and doc-comments may legitimately mention a skill.
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("///") || trimmed.StartsWith("*")) continue;

                // A comparison against a named skill is the smell: `== SkillType.Foraging`,
                // `is SkillType.Mining`, `case SkillType.Fishing`.
                if (System.Text.RegularExpressions.Regex.IsMatch(
                        line, @"(==|!=|\bis\b|\bcase\b)\s*SkillType\.\w+"))
                {
                    offenders.Add($"{Path.GetFileName(file)}:{i + 1}  {line.Trim()}");
                }
            }
        }

        Assert.That(offenders, Is.Empty,
            "service code must not branch on a specific skill:\n" + string.Join("\n", offenders));
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GeoSlayer.sln")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
