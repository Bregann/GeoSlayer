using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Services.Fog;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Services.Materials;

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

        var totals = new Dictionary<int, int>();

        foreach (var cell in cells)
        {
            var terrain = await GetOrClassifyTerrain(cell.GridLat, cell.GridLng, ct);

            var multiplier = terrain == TerrainType.Open ? 1.0 : MatchingTerrainMultiplier;

            var drops = DropRoller.Roll(
                playerId, cell.GridLat, cell.GridLng, terrain, entries, skillLevels,
                multiplier, toolTier);

            foreach (var drop in drops)
                totals[drop.MaterialId] = totals.GetValueOrDefault(drop.MaterialId) + drop.Quantity;
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
        return await db.PlayerItems
            .Include(pi => pi.Item)
            .Where(pi => pi.PlayerId == playerId
                      && pi.IsEquipped
                      && pi.Quantity > 0
                      && pi.Item.Modifier == ItemModifier.StackCapPercent)
            .SumAsync(pi => pi.Item.ModifierValue, ct);
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
