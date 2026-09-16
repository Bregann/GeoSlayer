using GeoSlayer.Domain.DTOs.Crafting.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Interfaces.Api;

/// <summary>
/// Crafting, gear and buildings (Stage 06, DESIGN.md §4.2–4.3).
/// </summary>
public interface ICraftingService
{
    /// <summary>
    /// Every recipe, with what the player holds and why a locked one is locked.
    ///
    /// Locked recipes are returned rather than filtered out: §4.2 wants them greyed with
    /// the requirement visible, so the player can always see what they are working toward.
    /// </summary>
    Task<RecipeListDto> GetRecipes(int playerId, CancellationToken ct);

    /// <summary>
    /// Queue a craft. Inputs are consumed now, not on completion — otherwise a player
    /// could queue everything and spend the materials elsewhere before it finishes.
    /// </summary>
    Task<CraftDto> QueueCraft(int playerId, string recipeKey, CancellationToken ct);

    /// <summary>Cancel a running craft and refund its inputs in full.</summary>
    Task CancelCraft(int playerId, int craftId, CancellationToken ct);

    /// <summary>
    /// Collect everything finished, evaluated lazily on sync — no recurring job, same
    /// reasoning as worker accrual (§5.3).
    /// </summary>
    Task<CraftCollectionDto> CollectCompletedCrafts(int playerId, CancellationToken ct);

    Task<List<CraftDto>> GetQueue(int playerId, CancellationToken ct);

    Task<List<PlayerItemDto>> GetItems(int playerId, CancellationToken ct);

    /// <summary>Equip or unequip gear, or place a building on a Claim.</summary>
    Task<List<PlayerItemDto>> SetEquipped(
        int playerId, int playerItemId, bool equipped, int? claimId, CancellationToken ct);

    /// <summary>
    /// Total value of a modifier from everything currently equipped or placed.
    ///
    /// The read path for systems that must honour gear — §4.3 is explicit that an
    /// equipped item changing no behaviour is a bug.
    /// </summary>
    Task<double> GetModifierTotal(int playerId, ItemModifier modifier, CancellationToken ct);
}
