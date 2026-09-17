using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Services.Fog;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Services.Materials
{
    /// <summary>
    /// Materials, stack caps and cell drops (Stage 03, DESIGN.md §4.1, §4.1a, §7.4).
    /// </summary>
    public class MaterialService(
        AppDbContext db,
        ITerrainClassifier classifier) : IMaterialService
    {
        /// <summary>
        /// How strongly a matching terrain boosts yield. This is the <i>only</i> thing
        /// geography controls — never which tiers are reachable (§4.1a).
        /// </summary>
        private const double MatchingTerrainMultiplier = 1.5;

        /// <summary>Flag at 90% of cap so the app can warn before anything is lost.</summary>
        private const double NearCapFraction = 0.9;

        public async Task<TerrainType> GetOrClassifyTerrain(int gridLat, int gridLng, CancellationToken ct)
        {
            var cached = await db.CellTerrains
                .FirstOrDefaultAsync(c => c.GridLat == gridLat && c.GridLng == gridLng, ct);

            if (cached is not null) return cached.Terrain;

            var terrain = await classifier.Classify(gridLat, gridLng, ct);

            db.CellTerrains.Add(new CellTerrain
            {
                GridLat = gridLat,
                GridLng = gridLng,
                Terrain = terrain,
                ClassifiedAtUtc = DateTime.UtcNow,
            });

            // Saved immediately so a concurrent reveal of the same cell finds the row rather
            // than classifying it again. The unique index makes a lost race harmless.
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Another request cached it first — take theirs and drop ours.
                db.ChangeTracker.Clear();

                var raced = await db.CellTerrains
                    .FirstOrDefaultAsync(c => c.GridLat == gridLat && c.GridLng == gridLng, ct);

                return raced?.Terrain ?? terrain;
            }

            return terrain;
        }

        /// <summary>
        /// Highest tier the player's equipped tool permits (§4.3), or null when they have no
        /// tool equipped.
        ///
        /// <para>Null means <b>no cap</b>, deliberately. Tools gate access for skills that
        /// have them; applying a cap to a player with no tool would silently lock every skill
        /// behind gear the tutorial does not teach. Stage 06 built the tools and left the gate
        /// to Stage 10 for exactly this reason.</para>
        /// </summary>
        private async Task<int?> EquippedToolTier(int playerId, CancellationToken ct)
        {
            var tiers = await db.PlayerItems
                .Include(pi => pi.Item)
                .Where(pi => pi.PlayerId == playerId
                          && pi.IsEquipped
                          && pi.Quantity > 0
                          && pi.Item.Modifier == ItemModifier.ToolTier)
                .Select(pi => pi.Item.ModifierValue)
                .ToListAsync(ct);

            return tiers.Count == 0 ? null : (int)tiers.Max();
        }

        /// <summary>
        /// How much faster an equipped tool gathers within a tier (§4.3, Stage 11).
        ///
        /// <para>A walk cannot block on a gather timer, so speed shows up as <b>more units per
        /// cell</b> — the same trick §4.1a uses to express <c>BaseGatherSeconds</c> on foot.
        /// This is what makes a better tool worth crafting once you already have one that
        /// reaches your tier.</para>
        /// </summary>
        private async Task<double> EquippedGatherSpeed(int playerId, CancellationToken ct)
        {
            var values = await db.PlayerItems
                .Include(pi => pi.Item)
                .Where(pi => pi.PlayerId == playerId && pi.IsEquipped && pi.Quantity > 0)
                .Select(pi => new { pi.Item.Modifier, pi.Item.ModifierValue,
                                    pi.Item.SecondaryModifier, pi.Item.SecondaryModifierValue })
                .ToListAsync(ct);

            return values.Sum(i =>
                (i.Modifier == ItemModifier.GatherSpeedPercent ? i.ModifierValue : 0)
                + (i.SecondaryModifier == ItemModifier.GatherSpeedPercent ? i.SecondaryModifierValue : 0));
        }

        public async Task<List<MaterialGainDto>> AwardCellDrops(
            int playerId, IReadOnlyList<GridCell> cells, CancellationToken ct)
        {
            if (cells.Count == 0) return [];

            var entries = await db.DropTableEntries
                .Include(e => e.Material)
                .ToListAsync(ct);

            if (entries.Count == 0) return [];

            // A skill missing here is treated as locked, so its materials cannot drop —
            // which is what keeps LevelRequired absolute rather than merely unlikely.
            var skillLevels = await db.PlayerSkills
                .Where(s => s.PlayerId == playerId)
                .ToDictionaryAsync(s => s.SkillType, s => s.Level, ct);

            var toolTier = await EquippedToolTier(playerId, ct);
            var gatherSpeed = await EquippedGatherSpeed(playerId, ct);

            var totals = new Dictionary<int, int>();

            foreach (var cell in cells)
            {
                var terrain = await GetOrClassifyTerrain(cell.GridLat, cell.GridLng, ct);

                var multiplier = terrain == TerrainType.Open ? 1.0 : MatchingTerrainMultiplier;

                var drops = DropRoller.Roll(
                    playerId, cell.GridLat, cell.GridLng, terrain, entries, skillLevels,
                    multiplier, toolTier);

                foreach (var drop in drops)
                {
                    // A faster tool yields more per cell. Floored at the base quantity so a
                    // rounding-down can never make a tool worse than none.
                    var quantity = gatherSpeed > 0
                        ? Math.Max(drop.Quantity, (int)Math.Round(drop.Quantity * (1 + gatherSpeed)))
                        : drop.Quantity;

                    totals[drop.MaterialId] = totals.GetValueOrDefault(drop.MaterialId) + quantity;
                }
            }

            return await GrantMaterials(playerId, totals, ct);
        }

        /// <summary>
        /// A player's effective cap for a material, including any Storehouse placed on a
        /// Claim (§4.3 buildings raise stack caps).
        ///
        /// Read directly rather than through ICraftingService to avoid a service cycle —
        /// FogService already depends on both.
        /// </summary>
        private async Task<double> StackCapBonus(int playerId, CancellationToken ct)
        {
            var fromItems = await db.PlayerItems
                .Include(pi => pi.Item)
                .Where(pi => pi.PlayerId == playerId
                          && pi.IsEquipped
                          && pi.Quantity > 0
                          && pi.Item.Modifier == ItemModifier.StackCapPercent)
                .SumAsync(pi => pi.Item.ModifierValue, ct);

            // A completed Museum wing grants a small permanent bonus (§5A). Read here rather
            // than through IMuseumService to avoid a service cycle — a bonus nothing reads
            // would be exactly the "displays but does nothing" bug §4.3 warns about.
            var completedWings = await CompletedMuseumWings(playerId, ct);

            return fromItems
                 + Services.Museum.MuseumSetBonus.TotalFor(ItemModifier.StackCapPercent, completedWings);
        }

        /// <summary>Wings the player has filled, for set bonuses.</summary>
        private async Task<List<MuseumWing>> CompletedMuseumWings(int playerId, CancellationToken ct)
        {
            var definitions = await db.MuseumEntryDefinitions
                .Select(d => new { d.Wing, d.Key })
                .ToListAsync(ct);

            if (definitions.Count == 0) return [];

            var found = (await db.PlayerMuseumEntries
                    .Where(e => e.PlayerId == playerId)
                    .Select(e => e.EntryKey)
                    .ToListAsync(ct))
                .ToHashSet();

            return definitions
                .GroupBy(d => d.Wing)
                .Where(g => g.All(d => found.Contains(d.Key)))
                .Select(g => g.Key)
                .ToList();
        }

        public async Task<List<MaterialGainDto>> GrantMaterials(
            int playerId, IReadOnlyDictionary<int, int> quantityByMaterialId, CancellationToken ct)
        {
            if (quantityByMaterialId.Count == 0) return [];

            var materialIds = quantityByMaterialId.Keys.ToList();

            var materials = await db.Materials
                .Where(m => materialIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, ct);

            var dust = await db.Materials.FirstOrDefaultAsync(m => m.Key == MaterialSeedData.DustKey, ct);

            var existing = await db.PlayerMaterials
                .Where(pm => pm.PlayerId == playerId)
                .ToDictionaryAsync(pm => pm.MaterialId, ct);

            var capBonus = await StackCapBonus(playerId, ct);

            var gains = new List<MaterialGainDto>();
            var dustFromOverflow = 0L;

            foreach (var (materialId, requested) in quantityByMaterialId)
            {
                if (requested <= 0) continue;
                if (!materials.TryGetValue(materialId, out var material)) continue;

                var row = existing.GetValueOrDefault(materialId);

                if (row is null)
                {
                    row = new PlayerMaterial { PlayerId = playerId, MaterialId = materialId, Quantity = 0 };
                    db.PlayerMaterials.Add(row);
                    existing[materialId] = row;
                }

                var cap = EffectiveCap(material.StackCap, capBonus);
                var space = Math.Max(0, cap - row.Quantity);
                var accepted = (int)Math.Min(requested, space);
                var overflow = requested - accepted;

                row.Quantity += accepted;

                // Overflow becomes Dust rather than being discarded or blocking the gather
                // (§7.4). Gathering at cap still succeeds — it just pays worse.
                if (overflow > 0 && dust is not null && material.Id != dust.Id)
                    dustFromOverflow += (long)overflow * material.DustPerOverflow;

                gains.Add(new MaterialGainDto
                {
                    MaterialId = material.Id,
                    Key = material.Key,
                    Name = material.Name,
                    Tier = material.Tier,
                    Category = material.Category,
                    Quantity = accepted,
                    OverflowConvertedToDust = overflow,
                });
            }

            if (dustFromOverflow > 0 && dust is not null)
            {
                var dustRow = existing.GetValueOrDefault(dust.Id);

                if (dustRow is null)
                {
                    dustRow = new PlayerMaterial { PlayerId = playerId, MaterialId = dust.Id, Quantity = 0 };
                    db.PlayerMaterials.Add(dustRow);
                    existing[dust.Id] = dustRow;
                }

                // Dust has its own (very large) cap, so it is clamped like anything else
                // rather than being allowed to grow without bound.
                dustRow.Quantity = Math.Min(
                    EffectiveCap(dust.StackCap, capBonus), dustRow.Quantity + dustFromOverflow);

                var dustGain = gains.FirstOrDefault(g => g.MaterialId == dust.Id);

                if (dustGain is null)
                {
                    gains.Add(new MaterialGainDto
                    {
                        MaterialId = dust.Id,
                        Key = dust.Key,
                        Name = dust.Name,
                        Tier = dust.Tier,
                        Category = dust.Category,
                        Quantity = (int)dustFromOverflow,
                    });
                }
                else
                {
                    dustGain.Quantity += (int)dustFromOverflow;
                }
            }

            await db.SaveChangesAsync(ct);
            return gains;
        }

        /// <summary>Base cap raised by any stack-cap bonus, floored at the base.</summary>
        private static long EffectiveCap(int baseCap, double bonus) =>
            (long)Math.Max(baseCap, Math.Floor(baseCap * (1 + Math.Max(0, bonus))));

        public async Task<InventoryDto> GetInventory(int playerId, CancellationToken ct)
        {
            var capBonus = await StackCapBonus(playerId, ct);

            var rows = await db.PlayerMaterials
                .Include(pm => pm.Material)
                .Where(pm => pm.PlayerId == playerId && pm.Quantity > 0)
                .ToListAsync(ct);

            var items = rows
                .Select(pm => new InventoryItemDto
                {
                    MaterialId = pm.MaterialId,
                    Key = pm.Material.Key,
                    Name = pm.Material.Name,
                    Tier = pm.Material.Tier,
                    Category = pm.Material.Category,
                    SkillType = pm.Material.SkillType,
                    Quantity = pm.Quantity,
                    StackCap = (int)EffectiveCap(pm.Material.StackCap, capBonus),
                    IsUnique = pm.Material.IsUnique,
                    IsNearCap = pm.Quantity >= EffectiveCap(pm.Material.StackCap, capBonus) * NearCapFraction,
                    IsFull = pm.Quantity >= EffectiveCap(pm.Material.StackCap, capBonus),
                })
                .ToList();

            var categories = items
                .GroupBy(i => i.Category)
                .OrderBy(g => g.Key)
                .Select(g => new InventoryCategoryDto
                {
                    Category = g.Key,
                    Name = g.Key.ToString(),
                    Items = g.OrderBy(i => i.Tier).ThenBy(i => i.Name).ToList(),
                })
                .ToList();

            return new InventoryDto
            {
                Categories = categories,
                DistinctMaterials = items.Count,
                NearCapCount = items.Count(i => i.IsNearCap),
            };
        }
    }
}
