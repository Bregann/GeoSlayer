using GeoSlayer.Domain.DTOs.Admin.Requests;
using GeoSlayer.Domain.DTOs.Admin.Responses;

namespace GeoSlayer.Domain.Interfaces.Api.Admin
{
    /// <summary>
    /// Game management (Stage 18).
    ///
    /// <para>Every method here mutates seeded balance data or player state, so every method
    /// takes the acting admin's identity and writes an audit entry. That is not optional
    /// bookkeeping: an admin action that changes a player's balance and leaves no record is
    /// indistinguishable from a bug.</para>
    /// </summary>
    public interface IAdminService
    {
        /// <summary>Every item, with its effect text and whether its modifier is read.</summary>
        Task<List<AdminItemDto>> GetItems(CancellationToken ct);

        /// <summary>Create or update an item.</summary>
        Task<AdminItemDto> SaveItem(string adminUserId, SaveItemRequest request, CancellationToken ct);

        /// <summary>Delete an item, its image, and any player copies of it.</summary>
        Task DeleteItem(string adminUserId, int itemId, CancellationToken ct);

        /// <summary>Store or replace an item's image. Validates the bytes, not the claim.</summary>
        Task UploadItemImage(
            string adminUserId, int itemId, byte[] data, string? contentType, string? fileName,
            CancellationToken ct);

        /// <summary>Remove an item's image.</summary>
        Task DeleteItemImage(string adminUserId, int itemId, CancellationToken ct);

        /// <summary>An item's image bytes and content type, or null when it has none.</summary>
        Task<(byte[] Data, string ContentType)?> GetItemImage(int itemId, CancellationToken ct);

        /// <summary>Every material, with its derived price and XP rate.</summary>
        Task<List<AdminMaterialDto>> GetMaterials(CancellationToken ct);

        /// <summary>
        /// Create or update a material.
        ///
        /// <para>Refuses anything the seed-data tests would reject — see
        /// <c>MaterialValidation</c>. A test only runs in CI, so the rules have to be
        /// restated where a runtime save can be stopped.</para>
        /// </summary>
        Task<AdminMaterialDto> SaveMaterial(
            string adminUserId, SaveMaterialRequest request, CancellationToken ct);

        /// <summary>Delete a material, refusing when anything still references it.</summary>
        Task DeleteMaterial(string adminUserId, int materialId, CancellationToken ct);

        /// <summary>Every recipe, with its cost, value and any warnings.</summary>
        Task<List<AdminRecipeDto>> GetRecipes(CancellationToken ct);

        /// <summary>
        /// Create or update a recipe.
        ///
        /// <para>Refuses anything structurally broken; merely questionable numbers come back
        /// as warnings on the result instead. See <c>RecipeValidation</c> for that split.</para>
        /// </summary>
        Task<AdminRecipeDto> SaveRecipe(
            string adminUserId, SaveRecipeRequest request, CancellationToken ct);

        /// <summary>Delete a recipe, and the inputs belonging to it.</summary>
        Task DeleteRecipe(string adminUserId, int recipeId, CancellationToken ct);

        /// <summary>Every encounter definition, with any set-level warnings.</summary>
        Task<List<AdminEncounterDto>> GetEncounters(CancellationToken ct);

        /// <summary>
        /// Create or update an encounter definition.
        ///
        /// <para>Refused when the <i>resulting set</i> would leave a Combat tier reachable
        /// only on historic ground — §5C.2's rule that a castle is a boost, never the only
        /// venue.</para>
        /// </summary>
        Task<AdminEncounterDto> SaveEncounter(
            string adminUserId, SaveEncounterRequest request, CancellationToken ct);

        /// <summary>Delete an encounter definition, if the set survives without it.</summary>
        Task DeleteEncounter(string adminUserId, int encounterId, CancellationToken ct);

        /// <summary>The audit trail, newest first.</summary>
        Task<List<AdminAuditDto>> GetAuditTrail(int limit, CancellationToken ct);
    }
}
