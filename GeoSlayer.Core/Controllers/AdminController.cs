using GeoSlayer.Domain.DTOs.Admin.Requests;
using GeoSlayer.Domain.DTOs.Admin.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api.Admin;
using GeoSlayer.Domain.Interfaces.Helpers;
using GeoSlayer.Domain.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GeoSlayer.Core.Controllers
{
    /// <summary>
    /// Game management (Stage 18).
    ///
    /// <para><b>Admin-only, enforced at the controller.</b> A management interface any
    /// logged-in player can reach is a way to give yourself a Royal Charter. The role comes
    /// from a claim in the JWT — see <see cref="AuthService.AdminRole"/> — and the attribute
    /// sits here rather than on each action so a new endpoint is protected by default rather
    /// than by remembering.</para>
    /// </summary>
    [ApiController]
    [Authorize(Roles = AuthService.AdminRole)]
    [Route("api/[controller]/[action]")]
    public class AdminController(
        IAdminService admin,
        IUserContextHelper userContextHelper) : ControllerBase
    {
        /// <summary>Every item, with its effect text and whether its modifier is read.</summary>
        [HttpGet]
        public async Task<ActionResult<List<AdminItemDto>>> GetItems(CancellationToken ct) =>
            Ok(await admin.GetItems(ct));

        /// <summary>Create or update an item.</summary>
        [HttpPost]
        public async Task<ActionResult<AdminItemDto>> SaveItem(
            [FromBody] SaveItemRequest request, CancellationToken ct) =>
            Ok(await admin.SaveItem(CurrentUserId(), request, ct));

        /// <summary>Delete an item.</summary>
        [HttpDelete]
        public async Task<ActionResult> DeleteItem([FromQuery] int itemId, CancellationToken ct)
        {
            await admin.DeleteItem(CurrentUserId(), itemId, ct);
            return Ok();
        }

        /// <summary>
        /// Upload or replace an item's image.
        ///
        /// <para>Metadata as a form field and the binary as a separate <see cref="IFormFile"/>,
        /// following Orbit's <c>DocumentsController</c>. The service takes the bytes rather
        /// than the <see cref="IFormFile"/> so it stays testable without ASP.NET types.</para>
        /// </summary>
        [HttpPost]
        [RequestSizeLimit(4 * 1024 * 1024)]
        public async Task<ActionResult> UploadItemImage(
            [FromForm] int itemId, IFormFile file, CancellationToken ct)
        {
            if (file is null || file.Length == 0)
            {
                throw new BadRequestException("No file provided.");
            }

            using var memory = new MemoryStream();
            await file.CopyToAsync(memory, ct);

            await admin.UploadItemImage(
                CurrentUserId(), itemId, memory.ToArray(), file.ContentType, file.FileName, ct);

            return Ok();
        }

        /// <summary>Remove an item's image.</summary>
        [HttpDelete]
        public async Task<ActionResult> DeleteItemImage([FromQuery] int itemId, CancellationToken ct)
        {
            await admin.DeleteItemImage(CurrentUserId(), itemId, ct);
            return Ok();
        }

        /// <summary>
        /// An item's image.
        ///
        /// <para><see cref="AllowAnonymous"/> deliberately: item art is not secret, it is
        /// rendered in the player app, and requiring a token would mean the app could not
        /// use an ordinary image element. The content type is the validated one stored at
        /// upload, never a client-supplied value.</para>
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public async Task<ActionResult> GetItemImage([FromQuery] int itemId, CancellationToken ct)
        {
            var image = await admin.GetItemImage(itemId, ct);

            if (image is null)
            {
                return NotFound();
            }

            return File(image.Value.Data, image.Value.ContentType);
        }

        /// <summary>Every material, with its derived price and XP rate.</summary>
        [HttpGet]
        public async Task<ActionResult<List<AdminMaterialDto>>> GetMaterials(CancellationToken ct) =>
            Ok(await admin.GetMaterials(ct));

        /// <summary>
        /// Create or update a material.
        ///
        /// <para>Refused when it would break an invariant the seed-data tests enforce —
        /// the response says which one.</para>
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<AdminMaterialDto>> SaveMaterial(
            [FromBody] SaveMaterialRequest request, CancellationToken ct) =>
            Ok(await admin.SaveMaterial(CurrentUserId(), request, ct));

        /// <summary>Delete a material.</summary>
        [HttpDelete]
        public async Task<ActionResult> DeleteMaterial([FromQuery] int materialId, CancellationToken ct)
        {
            await admin.DeleteMaterial(CurrentUserId(), materialId, ct);
            return Ok();
        }

        /// <summary>Every recipe, with its cost, value and any warnings.</summary>
        [HttpGet]
        public async Task<ActionResult<List<AdminRecipeDto>>> GetRecipes(CancellationToken ct) =>
            Ok(await admin.GetRecipes(ct));

        /// <summary>
        /// Create or update a recipe.
        ///
        /// <para>Structurally broken recipes are refused; questionable numbers come back as
        /// warnings on the result so an admin mid-tune is not fought by the interface.</para>
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<AdminRecipeDto>> SaveRecipe(
            [FromBody] SaveRecipeRequest request, CancellationToken ct) =>
            Ok(await admin.SaveRecipe(CurrentUserId(), request, ct));

        /// <summary>Delete a recipe.</summary>
        [HttpDelete]
        public async Task<ActionResult> DeleteRecipe([FromQuery] int recipeId, CancellationToken ct)
        {
            await admin.DeleteRecipe(CurrentUserId(), recipeId, ct);
            return Ok();
        }

        /// <summary>Every encounter definition, with any set-level warnings.</summary>
        [HttpGet]
        public async Task<ActionResult<List<AdminEncounterDto>>> GetEncounters(CancellationToken ct) =>
            Ok(await admin.GetEncounters(ct));

        /// <summary>
        /// Create or update an encounter definition.
        ///
        /// <para>Refused when the resulting set would leave a Combat tier reachable only on
        /// historic ground (§5C.2).</para>
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<AdminEncounterDto>> SaveEncounter(
            [FromBody] SaveEncounterRequest request, CancellationToken ct) =>
            Ok(await admin.SaveEncounter(CurrentUserId(), request, ct));

        /// <summary>Delete an encounter definition.</summary>
        [HttpDelete]
        public async Task<ActionResult> DeleteEncounter([FromQuery] int encounterId, CancellationToken ct)
        {
            await admin.DeleteEncounter(CurrentUserId(), encounterId, ct);
            return Ok();
        }

        /// <summary>The unlock ladder and upgrade tree.</summary>
        [HttpGet]
        public async Task<ActionResult<AdminProgressionDto>> GetProgression(CancellationToken ct) =>
            Ok(await admin.GetProgression(ct));

        /// <summary>
        /// Create or update a Bonus Point upgrade.
        ///
        /// <para>The cost curve is validated before storage — it is parsed with
        /// <c>int.Parse</c> on every upgrades-screen load.</para>
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<AdminUpgradeDto>> SaveUpgrade(
            [FromBody] SaveUpgradeRequest request, CancellationToken ct) =>
            Ok(await admin.SaveUpgrade(CurrentUserId(), request, ct));

        /// <summary>Delete an upgrade.</summary>
        [HttpDelete]
        public async Task<ActionResult> DeleteUpgrade([FromQuery] int upgradeId, CancellationToken ct)
        {
            await admin.DeleteUpgrade(CurrentUserId(), upgradeId, ct);
            return Ok();
        }

        /// <summary>Create or update one rung of the unlock ladder.</summary>
        [HttpPost]
        public async Task<ActionResult<AdminUnlockDto>> SaveUnlock(
            [FromBody] SaveUnlockRequest request, CancellationToken ct) =>
            Ok(await admin.SaveUnlock(CurrentUserId(), request, ct));

        /// <summary>Delete an unlock rung.</summary>
        [HttpDelete]
        public async Task<ActionResult> DeleteUnlock([FromQuery] int unlockId, CancellationToken ct)
        {
            await admin.DeleteUnlock(CurrentUserId(), unlockId, ct);
            return Ok();
        }

        /// <summary>Every tunable number, grouped by category.</summary>
        [HttpGet]
        public async Task<ActionResult<List<AdminGameSettingDto>>> GetGameSettings(CancellationToken ct) =>
            Ok(await admin.GetGameSettings(ct));

        /// <summary>
        /// Change one tunable number.
        ///
        /// <para>Validated against the setting's own bounds, then applied immediately — a
        /// change needing a restart would be no better than the constant it replaced.</para>
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<AdminGameSettingDto>> SaveGameSetting(
            [FromBody] SaveGameSettingRequest request, CancellationToken ct) =>
            Ok(await admin.SaveGameSetting(CurrentUserId(), request, ct));

        /// <summary>Every Museum entry, with how many players have found it.</summary>
        [HttpGet]
        public async Task<ActionResult<List<AdminMuseumEntryDto>>> GetMuseumEntries(CancellationToken ct) =>
            Ok(await admin.GetMuseumEntries(ct));

        /// <summary>Create or update a Museum entry definition.</summary>
        [HttpPost]
        public async Task<ActionResult<AdminMuseumEntryDto>> SaveMuseumEntry(
            [FromBody] SaveMuseumEntryRequest request, CancellationToken ct) =>
            Ok(await admin.SaveMuseumEntry(CurrentUserId(), request, ct));

        /// <summary>Delete a Museum entry.</summary>
        [HttpDelete]
        public async Task<ActionResult> DeleteMuseumEntry([FromQuery] int entryId, CancellationToken ct)
        {
            await admin.DeleteMuseumEntry(CurrentUserId(), entryId, ct);
            return Ok();
        }

        /// <summary>Find players by username or email.</summary>
        [HttpGet]
        public async Task<ActionResult<List<AdminPlayerSummaryDto>>> SearchPlayers(
            [FromQuery] string query, CancellationToken ct) =>
            Ok(await admin.SearchPlayers(query, ct));

        /// <summary>Everything about one player. Read-only.</summary>
        [HttpGet]
        public async Task<ActionResult<AdminPlayerDto>> GetPlayer(
            [FromQuery] int playerId, CancellationToken ct) =>
            Ok(await admin.GetPlayer(playerId, ct));

        /// <summary>
        /// Upload or replace a sprite for an item, material, encounter or Museum entry.
        /// </summary>
        [HttpPost]
        [RequestSizeLimit(4 * 1024 * 1024)]
        public async Task<ActionResult> UploadSprite(
            [FromForm] SpriteOwner ownerType, [FromForm] int ownerId, IFormFile file,
            CancellationToken ct)
        {
            if (file is null || file.Length == 0)
            {
                throw new BadRequestException("No file provided.");
            }

            using var memory = new MemoryStream();
            await file.CopyToAsync(memory, ct);

            await admin.UploadSprite(
                CurrentUserId(), ownerType, ownerId, memory.ToArray(),
                file.ContentType, file.FileName, ct);

            return Ok();
        }

        /// <summary>Remove a sprite.</summary>
        [HttpDelete]
        public async Task<ActionResult> DeleteSprite(
            [FromQuery] SpriteOwner ownerType, [FromQuery] int ownerId, CancellationToken ct)
        {
            await admin.DeleteSprite(CurrentUserId(), ownerType, ownerId, ct);
            return Ok();
        }

        /// <summary>
        /// A sprite.
        ///
        /// <para><see cref="AllowAnonymous"/> for the same reason item images are: artwork is
        /// not secret, it is rendered in the player app, and requiring a token would stop the
        /// app using an ordinary image element.</para>
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public async Task<ActionResult> GetSprite(
            [FromQuery] SpriteOwner ownerType, [FromQuery] int ownerId, CancellationToken ct)
        {
            var sprite = await admin.GetSprite(ownerType, ownerId, ct);

            if (sprite is null)
            {
                return NotFound();
            }

            return File(sprite.Value.Data, sprite.Value.ContentType);
        }

        /// <summary>
        /// Add or remove a player's coin.
        ///
        /// <para>Uncapped — an admin is trusted, and the audit trail is the control. A reason
        /// is required, because an entry without one cannot answer the question it exists
        /// for.</para>
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<AdminPlayerDto>> AdjustPlayerCoin(
            [FromBody] AdjustPlayerRequest request, CancellationToken ct) =>
            Ok(await admin.AdjustPlayerCoin(CurrentUserId(), request, ct));

        /// <summary>Add or remove a material from a player's inventory.</summary>
        [HttpPost]
        public async Task<ActionResult<AdminPlayerDto>> AdjustPlayerMaterial(
            [FromBody] AdjustPlayerMaterialRequest request, CancellationToken ct) =>
            Ok(await admin.AdjustPlayerMaterial(CurrentUserId(), request, ct));

        /// <summary>Add or remove an item from a player's inventory.</summary>
        [HttpPost]
        public async Task<ActionResult<AdminPlayerDto>> AdjustPlayerItem(
            [FromBody] AdjustPlayerItemRequest request, CancellationToken ct) =>
            Ok(await admin.AdjustPlayerItem(CurrentUserId(), request, ct));

        /// <summary>The audit trail, newest first.</summary>
        [HttpGet]
        public async Task<ActionResult<List<AdminAuditDto>>> GetAuditTrail(
            [FromQuery] int limit = 100, CancellationToken ct = default) =>
            Ok(await admin.GetAuditTrail(limit, ct));

        /// <summary>
        /// The acting admin.
        ///
        /// <para>Taken from the token rather than a parameter — an audit trail whose author
        /// is supplied by the caller is a trail anyone can forge.</para>
        /// </summary>
        private string CurrentUserId() => userContextHelper.GetUserId();
    }
}
