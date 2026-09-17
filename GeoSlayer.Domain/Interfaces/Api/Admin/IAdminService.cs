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

        /// <summary>The unlock ladder and upgrade tree, with any ladder warnings.</summary>
        Task<AdminProgressionDto> GetProgression(CancellationToken ct);

        /// <summary>
        /// Create or update a Bonus Point upgrade.
        ///
        /// <para>The cost curve is validated before it is stored —
        /// <c>UpgradeDefinition.Costs</c> parses it with <c>int.Parse</c> on every
        /// upgrades-screen load, so a malformed value crashes that screen for everyone.</para>
        /// </summary>
        Task<AdminUpgradeDto> SaveUpgrade(
            string adminUserId, SaveUpgradeRequest request, CancellationToken ct);

        /// <summary>Delete an upgrade, refusing when players have already bought ranks.</summary>
        Task DeleteUpgrade(string adminUserId, int upgradeId, CancellationToken ct);

        /// <summary>Create or update one rung of the unlock ladder.</summary>
        Task<AdminUnlockDto> SaveUnlock(
            string adminUserId, SaveUnlockRequest request, CancellationToken ct);

        /// <summary>Delete an unlock rung.</summary>
        Task DeleteUnlock(string adminUserId, int unlockId, CancellationToken ct);

        /// <summary>Every tunable number, grouped by category.</summary>
        Task<List<AdminGameSettingDto>> GetGameSettings(CancellationToken ct);

        /// <summary>
        /// Change one tunable number.
        ///
        /// <para>Validated against the setting's own bounds, then the cache is reloaded so
        /// the change takes effect without a restart — which is the entire point of the
        /// table.</para>
        /// </summary>
        Task<AdminGameSettingDto> SaveGameSetting(
            string adminUserId, SaveGameSettingRequest request, CancellationToken ct);

        /// <summary>The audit trail, newest first.</summary>
        Task<List<AdminAuditDto>> GetAuditTrail(int limit, CancellationToken ct);
    }
}
