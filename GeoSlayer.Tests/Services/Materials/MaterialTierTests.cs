using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Skills;

namespace GeoSlayer.Tests.Services.Materials
{
    /// <summary>
    /// Stage 03 criteria 2, 9, 10, 11 and 12 — the tier rules from DESIGN.md §4.1a.
    ///
    /// These are pure: <see cref="DropRoller"/> takes its state as arguments, so the rules
    /// that matter most can be asserted thousands of times without a database.
    /// </summary>
    [TestFixture]
    public class MaterialTierTests
    {
        /// <summary>
        /// Every seeded material — terrain pools plus skill ladders.
        ///
        /// Stage 10 moved Mining's ore out of <see cref="MaterialSeedData"/> and into
        /// <see cref="SkillSeedData"/>, so a table built from one alone is now incomplete.
        /// </summary>
        private static IEnumerable<Material> AllMaterials =>
            MaterialSeedData.Materials.Concat(SkillSeedData.AllSkillMaterials);

        /// <summary>Build drop entries from the seed data, assigning ids as a database would.</summary>
        private static (List<DropTableEntry> Entries, Dictionary<string, Material> ByKey) BuildTable()
        {
            var byKey = new Dictionary<string, Material>();
            var id = 1;

            foreach (var material in AllMaterials)
            {
                var copy = new Material
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

                byKey[copy.Key] = copy;
            }

            var entries = new List<DropTableEntry>();
            var entryId = 1;
            var seen = new HashSet<(TerrainType, int)>();

            foreach (var (terrain, key, weight, min, max) in
                     MaterialSeedData.DropEntries().Concat(SkillSeedData.DropEntries()))
            {
                if (!byKey.TryGetValue(key, out var material))
                {
                    continue;
                }

                if (!seen.Add((terrain, material.Id)))
                {
                    continue;
                }

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

        // ── Criterion 2: the material budget ────────────────────────────

        [Test]
        public void SeededMaterials_StayWithinTheStageBudget()
        {
            // Stage 03's own budget. Per-skill ladders are counted against their own stages,
            // and each is a fixed seven tiers by construction.
            var total = MaterialSeedData.Materials.Count;
            var unique = MaterialSeedData.Materials.Count(m => m.IsUnique);

            Assert.Multiple(() =>
            {
                Assert.That(total, Is.LessThanOrEqualTo(25), "Stage 03 budget is 25 distinct materials");
                Assert.That(unique, Is.LessThanOrEqualTo(15), "and at most 15 unique named ones");
            });
        }

        [Test]
        public void MaterialKeys_AreUnique()
        {
            // Across everything, not just Stage 03's pools: a duplicate key between a
            // terrain pool and a skill ladder would leave one silently unseeded.
            var keys = AllMaterials.Select(m => m.Key).ToList();
            Assert.That(keys, Is.Unique);
        }

        // ── Criterion 12: XP/hour flat across tiers ─────────────────────

        [Test]
        public void XpPerHour_IsRoughlyFlatAcrossTiers()
        {
            // The rule from §4.1a: a higher BaseGatherSeconds must be offset by a higher
            // XpPerUnit, so advancing a tier is never a downgrade. Dust is excluded — it is
            // overflow currency and deliberately grants no XP.
            var gathered = MaterialSeedData.Materials
                .Where(m => m.Category != MaterialCategory.Dust)
                .ToList();

            Assert.That(gathered, Is.Not.Empty);

            foreach (var material in gathered)
            {
                var xpPerSecond = material.XpPerUnit / material.BaseGatherSeconds;

                Assert.That(xpPerSecond, Is.EqualTo(MaterialSeedData.XpPerGatherSecond).Within(0.01),
                    $"{material.Key} pays {xpPerSecond:F3} XP/sec, off the flat curve");
            }
        }

        [Test]
        public void HigherTiers_TakeLongerToGather()
        {
            foreach (var group in MaterialSeedData.Materials
                         .Where(m => m.Category != MaterialCategory.Dust)
                         .GroupBy(m => m.Category))
            {
                var ordered = group.OrderBy(m => m.Tier).ToList();

                for (var i = 1; i < ordered.Count; i++)
                {
                    Assert.That(ordered[i].BaseGatherSeconds, Is.GreaterThan(ordered[i - 1].BaseGatherSeconds),
                        $"{ordered[i].Key} should take longer than {ordered[i - 1].Key}");
                }
            }
        }

        [Test]
        public void HigherTiers_RequireHigherLevels()
        {
            foreach (var group in MaterialSeedData.Materials
                         .Where(m => m.Category != MaterialCategory.Dust)
                         .GroupBy(m => m.Category))
            {
                var ordered = group.OrderBy(m => m.Tier).ToList();

                for (var i = 1; i < ordered.Count; i++)
                {
                    Assert.That(ordered[i].LevelRequired, Is.GreaterThan(ordered[i - 1].LevelRequired),
                        $"{ordered[i].Key} should gate above {ordered[i - 1].Key}");
                }
            }
        }

        // ── Criterion 9: LevelRequired is absolute ──────────────────────

        [Test]
        public void ALevel19Player_NeverObtainsALevel20Material()
        {
            var (entries, byKey) = BuildTable();

            // Iron Ore requires Mining 20. Not rarely at 19 — never.
            var ironOre = byKey["ore_iron"];
            Assert.That(ironOre.LevelRequired, Is.EqualTo(20), "fixture assumption");

            var levels = new Dictionary<SkillType, int> { [SkillType.Mining] = 19 };

            // Many distinct cells, so this is not one lucky seed.
            for (var cell = 0; cell < 2_000; cell++)
            {
                var drops = DropRoller.Roll(
                    playerId: 1, gridLat: cell, gridLng: cell * 7,
                    TerrainType.Rocky, entries, levels);

                Assert.That(drops.Any(d => d.MaterialId == ironOre.Id), Is.False,
                    $"Iron Ore dropped at Mining 19 on cell {cell}");
            }
        }

        [Test]
        public void ALockedSkill_YieldsNoneOfItsMaterials()
        {
            var (entries, _) = BuildTable();

            // No skills at all — the player has unlocked nothing.
            var levels = new Dictionary<SkillType, int>();

            for (var cell = 0; cell < 200; cell++)
            {
                var drops = DropRoller.Roll(
                    playerId: 3, gridLat: cell, gridLng: -cell,
                    TerrainType.Rocky | TerrainType.Woodland, entries, levels);

                foreach (var drop in drops)
                {
                    var material = AllMaterials.First(m => m.Key == drop.MaterialKey);

                    Assert.That(material.SkillType, Is.Null,
                        $"{drop.MaterialKey} dropped with no skill unlocked");
                }
            }
        }

        // ── Criterion 10: unlocking makes it obtainable and favoured ────

        [Test]
        public void AtLevel20_IronOreBecomesObtainable()
        {
            var (entries, byKey) = BuildTable();
            var ironOre = byKey["ore_iron"];

            var levels = new Dictionary<SkillType, int> { [SkillType.Mining] = 20 };

            var found = false;

            for (var cell = 0; cell < 500 && !found; cell++)
            {
                var drops = DropRoller.Roll(
                    playerId: 2, gridLat: cell, gridLng: cell * 3,
                    TerrainType.Rocky, entries, levels);

                found = drops.Any(d => d.MaterialId == ironOre.Id);
            }

            Assert.That(found, Is.True, "Iron Ore should be obtainable at Mining 20");
        }

        [Test]
        public void TheHighestUnlockedTier_IsWeightedAboveLowerOnes()
        {
            var (entries, byKey) = BuildTable();

            // At Mining 20 the rocky ladder is Rough Stone (1), Copper (10), Iron (20).
            var levels = new Dictionary<SkillType, int> { [SkillType.Mining] = 20 };

            var counts = new Dictionary<int, int>();

            for (var cell = 0; cell < 3_000; cell++)
            {
                var drops = DropRoller.Roll(
                    playerId: 4, gridLat: cell, gridLng: cell * 11,
                    TerrainType.Rocky, entries, levels);

                foreach (var drop in drops)
                {
                    counts[drop.MaterialId] = counts.GetValueOrDefault(drop.MaterialId) + 1;
                }
            }

            var iron = counts.GetValueOrDefault(byKey["ore_iron"].Id);
            var stone = counts.GetValueOrDefault(byKey["stone_rough"].Id);

            Assert.Multiple(() =>
            {
                Assert.That(iron, Is.GreaterThan(0), "the top unlocked tier should appear");
                Assert.That(stone, Is.Zero,
                    "tier 1 is outside the top-two fallback band once tier 3 is unlocked");
            });
        }

        // ── Criterion 11: tiers gate on level, not geography ────────────

        [Test]
        public void WithNoMatchingTerrain_EveryUnlockedTierIsStillReachable()
        {
            var (entries, byKey) = BuildTable();

            // A player at Mining 50 living somewhere with no rocky terrain at all.
            var levels = new Dictionary<SkillType, int> { [SkillType.Mining] = 50 };

            var goldOre = byKey["ore_gold"];
            Assert.That(goldOre.LevelRequired, Is.EqualTo(50), "fixture assumption");

            // Rocky entries are reachable regardless of the cell's terrain: what you can
            // obtain depends only on skill level (§4.1a).
            // Mining's ladder moved to the Mined category in Stage 10; Rocky is now the
            // terrain, not the material pool.
            var obtainable = entries
                .Where(e => e.Material.SkillType == SkillType.Mining)
                .Where(e => DropRoller.IsObtainable(e.Material, levels))
                .Select(e => e.Material.Key)
                .ToList();

            Assert.That(obtainable, Contains.Item("ore_gold"),
                "a landlocked player at Mining 50 must still be able to obtain Gold Ore");
        }

        [Test]
        public void AnOpenCell_StillYieldsSomething()
        {
            var (entries, _) = BuildTable();

            // Criterion 4: an unclassified cell must never yield nothing — that would be a
            // geographic dead zone.
            var levels = new Dictionary<SkillType, int> { [SkillType.Trading] = 1 };

            for (var cell = 0; cell < 200; cell++)
            {
                var drops = DropRoller.Roll(
                    playerId: 5, gridLat: cell, gridLng: cell + 100,
                    TerrainType.Open, entries, levels);

                Assert.That(drops, Is.Not.Empty, $"open cell {cell} yielded nothing");
            }
        }

        // ── Criterion 6: deterministic rolls ────────────────────────────

        [Test]
        public void RollingTheSameCellTwice_YieldsIdenticalDrops()
        {
            var (entries, _) = BuildTable();
            var levels = new Dictionary<SkillType, int> { [SkillType.Mining] = 20 };

            var first = DropRoller.Roll(7, 100, 200, TerrainType.Rocky, entries, levels);
            var second = DropRoller.Roll(7, 100, 200, TerrainType.Rocky, entries, levels);

            Assert.That(second, Is.EqualTo(first), "a replayed roll must not re-roll");
        }

        [Test]
        public void DifferentPlayers_RollIndependently()
        {
            var (entries, _) = BuildTable();
            var levels = new Dictionary<SkillType, int> { [SkillType.Mining] = 20 };

            // Same cell, different players: the seeds must differ or every player on a cell
            // gets identical loot forever.
            var seedA = DropRoller.SeedFor(1, 50, 50);
            var seedB = DropRoller.SeedFor(2, 50, 50);

            Assert.That(seedA, Is.Not.EqualTo(seedB));
        }

        [Test]
        public void SeedFor_IsStableAndKnown()
        {
            // Pinning the literal is what makes this meaningful: comparing the function to
            // itself would pass even if it were randomised per process. If SeedFor's mixing
            // ever changes, every player's cell loot silently re-rolls — so that should be a
            // deliberate act that updates this number, not a silent regression.
            const int expected = 1193960218;

            Assert.That(DropRoller.SeedFor(42, -17, 900), Is.EqualTo(expected),
                "the seed must stay stable across builds and processes");
        }

        [Test]
        public void SeedFor_IsNonNegative()
        {
            // Random's constructor rejects int.MinValue, so negative coordinates must not
            // produce one.
            foreach (var (player, lat, lng) in new[] { (1, -1, -1), (int.MaxValue, -9999, -9999), (7, 0, 0) })
            {
                Assert.That(DropRoller.SeedFor(player, lat, lng), Is.GreaterThanOrEqualTo(0));
            }
        }
    }
}
