using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Fog;

namespace GeoSlayer.Domain.Interfaces.Api.Materials
{
    /// <summary>
    /// Materials, stack caps and cell drops (Stage 03).
    /// </summary>
    public interface IMaterialService
    {
        /// <summary>
        /// Terrain for a grid cell, classifying and caching it on first request.
        ///
        /// Cached globally rather than per player: terrain is a property of the world, so the
        /// second player to reveal a cell must reuse the stored row and trigger no new lookup.
        /// </summary>
        Task<TerrainType> GetOrClassifyTerrain(int gridLat, int gridLng, CancellationToken ct);

        /// <summary>
        /// Roll and award materials for newly revealed cells.
        ///
        /// Deterministic per (player, cell): replaying an identical sync yields identical
        /// drops rather than a fresh roll.
        /// </summary>
        Task<List<MaterialGainDto>> AwardCellDrops(
            int playerId, IReadOnlyList<GridCell> cells, CancellationToken ct);

        /// <summary>
        /// Add materials, honouring per-material stack caps.
        ///
        /// Overflow auto-converts to Dust at a poor rate (§7.4) — it never discards and never
        /// blocks, because the idle layer must not punish a player for sleeping.
        /// </summary>
        Task<List<MaterialGainDto>> GrantMaterials(
            int playerId, IReadOnlyDictionary<int, int> quantityByMaterialId, CancellationToken ct);

        /// <summary>The player's inventory, grouped for display.</summary>
        Task<InventoryDto> GetInventory(int playerId, CancellationToken ct);

        /// <summary>
        /// The player's fractional sell-price bonus (§5.4) — gear, buildings, Museum wings
        /// and Banking level combined.
        /// </summary>
        Task<double> SellPriceBonus(int playerId, CancellationToken ct);
    }
}
