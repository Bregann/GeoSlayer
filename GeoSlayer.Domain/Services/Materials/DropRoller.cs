using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Materials
{
    /// <summary>
    /// Chooses what a revealed cell yields (Stage 03 task 4, DESIGN.md §4.1a).
    ///
    /// Pure and deterministic: given the same player, cell, terrain and levels it returns the
    /// same drops. That is what makes a replayed sync unable to re-roll for a better result,
    /// and it is why this is a static class over a seeded RNG rather than a service holding
    /// <see cref="Random.Shared"/>.
    /// </summary>
    public static class DropRoller
    {
        /// <summary>One rolled drop, before stack caps are applied.</summary>
        public readonly record struct Drop(int MaterialId, string MaterialKey, int Quantity);

        /// <summary>
        /// A stable seed for one (player, cell) pair.
        ///
        /// Deliberately excludes time: re-revealing the same cell must not produce a new roll,
        /// or a client could replay a sync until it liked the result.
        /// </summary>
        public static int SeedFor(int playerId, int gridLat, int gridLng)
        {
            // FNV-1a over the three values. Cheap, well-mixed, and stable across processes —
            // unlike string GetHashCode, which is randomised per run in .NET Core.
            unchecked
            {
                const uint offset = 2166136261;
                const uint prime = 16777619;

                var hash = offset;

                Span<int> values = [playerId, gridLat, gridLng];

                foreach (var value in values)
                {
                    for (var shift = 0; shift < 32; shift += 8)
                    {
                        hash ^= (uint)((value >> shift) & 0xFF);
                        hash *= prime;
                    }
                }

                // Non-negative so it can seed System.Random without wrapping.
                return (int)(hash & 0x7FFFFFFF);
            }
        }

        /// <summary>
        /// Roll the drops for one revealed cell.
        /// </summary>
        /// <param name="skillLevels">
        /// Current level per gathering skill. A skill absent from this map is treated as
        /// locked, so its materials cannot drop at all.
        /// </param>
        /// <param name="terrainMultiplier">
        /// How much this terrain favours the material's pool. Terrain scales <i>quantity</i>
        /// only — never which tiers are reachable (§4.1a).
        /// </param>
        /// <param name="maxToolTier">
        /// Highest tier the player's equipped tool permits (§4.3: "tools gate access").
        ///
        /// <para>Null means no tool requirement applies — which is the correct default, not a
        /// permissive fallback. A tool gate is only meaningful for skills that have tools; a
        /// skill without one must never be silently capped.</para>
        /// </param>
        public static List<Drop> Roll(
            int playerId,
            int gridLat,
            int gridLng,
            TerrainType terrain,
            IReadOnlyList<DropTableEntry> entries,
            IReadOnlyDictionary<SkillType, int> skillLevels,
            double terrainMultiplier = 1.0,
            int? maxToolTier = null)
        {
            var random = new Random(SeedFor(playerId, gridLat, gridLng));

            // Entries for this cell's terrain. A cell can be several terrains at once, so the
            // flags are unioned rather than matched exactly, and Open is always included so
            // no cell can come back empty.
            var candidates = entries
                .Where(e => e.Terrain == TerrainType.Open
                         || (e.Terrain != TerrainType.Open && (terrain & e.Terrain) == e.Terrain))
                .Where(e => IsObtainable(e.Material, skillLevels))
                .Where(e => maxToolTier is null || e.Material.Tier <= maxToolTier.Value)
                .ToList();

            if (candidates.Count == 0)
            {
                return [];
            }

            // Highest unlocked tier wins, with weighted fallback below it (§4.1a). Selecting
            // per category keeps a woodland cell from crowding out its own top tier just
            // because the base pool has more entries.
            var drops = new List<Drop>();

            foreach (var group in candidates.GroupBy(e => e.Material.Category).OrderBy(g => g.Key))
            {
                var best = group.Max(e => e.Material.Tier);

                // Fallback band: the top tier plus the one below it, so progression still
                // feels like a mix rather than an abrupt switch.
                var band = group.Where(e => e.Material.Tier >= best - 1).ToList();

                var picked = PickWeighted(band, random);
                if (picked is null)
                {
                    continue;
                }

                var quantity = picked.MinQuantity;
                if (picked.MaxQuantity > picked.MinQuantity)
                {
                    quantity = random.Next(picked.MinQuantity, picked.MaxQuantity + 1);
                }

                // Terrain scales quantity. Floored at 1 so a poor-terrain match still yields
                // something — the whole point of the geography rule.
                quantity = Math.Max(1, (int)Math.Round(quantity * terrainMultiplier));

                drops.Add(new Drop(picked.MaterialId, picked.Material.Key, quantity));
            }

            return drops;
        }

        /// <summary>
        /// Whether the player can obtain this material at all.
        ///
        /// <see cref="Material.LevelRequired"/> is absolute (§4.1a): below it the material is
        /// not obtainable, not merely rare.
        /// </summary>
        public static bool IsObtainable(Material material, IReadOnlyDictionary<SkillType, int> skillLevels)
        {
            // No skill means a universal material (Dust) — always available.
            if (material.SkillType is null)
            {
                return material.LevelRequired <= 1;
            }

            return skillLevels.TryGetValue(material.SkillType.Value, out var level)
                && level >= material.LevelRequired;
        }

        private static DropTableEntry? PickWeighted(List<DropTableEntry> entries, Random random)
        {
            var total = entries.Sum(e => Math.Max(1, e.Weight));
            if (total <= 0)
            {
                return null;
            }

            var roll = random.Next(total);

            foreach (var entry in entries)
            {
                roll -= Math.Max(1, entry.Weight);
                if (roll < 0)
                {
                    return entry;
                }
            }

            return entries[^1];
        }
    }
}
