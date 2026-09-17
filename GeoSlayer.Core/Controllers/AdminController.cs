using GeoSlayer.Domain.DTOs.Admin.Requests;
using GeoSlayer.Domain.DTOs.Admin.Responses;
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
