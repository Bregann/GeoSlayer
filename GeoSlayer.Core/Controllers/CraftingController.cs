using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.DTOs.Crafting.Responses;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Interfaces.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Core.Controllers
{
    /// <summary>Crafting, gear and buildings (Stage 06).</summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]/[action]")]
    public class CraftingController(
        AppDbContext db,
        ICraftingService crafting,
        IUserContextHelper userContextHelper) : ControllerBase
    {
        /// <summary>
        /// Every recipe, including locked ones — §4.2 wants them greyed with the requirement
        /// visible, so the player can see what they are working toward.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<RecipeListDto>> GetRecipes(CancellationToken ct) =>
            Ok(await crafting.GetRecipes(await CurrentPlayerId(ct), ct));

        [HttpGet]
        public async Task<ActionResult<List<CraftDto>>> GetQueue(CancellationToken ct) =>
            Ok(await crafting.GetQueue(await CurrentPlayerId(ct), ct));

        [HttpPost]
        public async Task<ActionResult<CraftDto>> QueueCraft([FromQuery] string key, CancellationToken ct) =>
            Ok(await crafting.QueueCraft(await CurrentPlayerId(ct), key, ct));

        [HttpDelete]
        public async Task<IActionResult> CancelCraft([FromQuery] int id, CancellationToken ct)
        {
            await crafting.CancelCraft(await CurrentPlayerId(ct), id, ct);
            return NoContent();
        }

        [HttpGet]
        public async Task<ActionResult<List<PlayerItemDto>>> GetItems(CancellationToken ct) =>
            Ok(await crafting.GetItems(await CurrentPlayerId(ct), ct));

        /// <summary>Equip or unequip gear, or place a building on a Claim.</summary>
        [HttpPost]
        public async Task<ActionResult<List<PlayerItemDto>>> SetEquipped(
            [FromQuery] int id, [FromBody] EquipRequest request, CancellationToken ct) =>
            Ok(await crafting.SetEquipped(
                await CurrentPlayerId(ct), id, request.Equipped, request.ClaimId, ct));

        private async Task<int> CurrentPlayerId(CancellationToken ct)
        {
            var userId = userContextHelper.GetUserId();

            var playerId = await db.Players
                .Where(p => p.UserId == userId)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(ct);

            return playerId ?? throw new NotFoundException("Player not found");
        }
    }

    public class EquipRequest
    {
        public bool Equipped { get; set; }
        public int? ClaimId { get; set; }
    }
}
