using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Progression.Responses;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Interfaces.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Controllers
{
    /// <summary>
    /// Skills and the Bonus Point tree (Stage 02, DESIGN.md §3.0a / §3.1).
    ///
    /// As with Journey, the player is resolved from the JWT and never from the request —
    /// a client-supplied id would let one account spend another's points.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]/[action]")]
    public class PlayerController(
        AppDbContext db,
        IProgressionService progression,
        IMaterialService materials,
        IUserContextHelper userContextHelper) : ControllerBase
    {
        /// <summary>The caller's materials, grouped by category (Stage 03 task 5).</summary>
        [HttpGet]
        public async Task<ActionResult<InventoryDto>> GetInventory(CancellationToken ct)
        {
            return Ok(await materials.GetInventory(await CurrentPlayerId(ct), ct));
        }

        /// <summary>Unlocked skills plus the locked ladder ahead — the roadmap (§3.1c).</summary>
        [HttpGet]
        public async Task<ActionResult<PlayerSkillsDto>> GetSkills(CancellationToken ct)
        {
            var playerId = await CurrentPlayerId(ct);

            // Backstop for accounts created before the ladder was seeded.
            await progression.EnsureStartingUnlocks(playerId, ct);

            return Ok(await progression.GetSkills(playerId, ct));
        }

        /// <summary>The Bonus Point tree with the caller's ranks and affordability.</summary>
        [HttpGet]
        public async Task<ActionResult<PlayerUpgradesDto>> GetUpgrades(CancellationToken ct)
        {
            return Ok(await progression.GetUpgrades(await CurrentPlayerId(ct), ct));
        }

        /// <summary>Buy one rank of an upgrade.</summary>
        [HttpPost]
        public async Task<ActionResult<PlayerUpgradesDto>> PurchaseUpgrade([FromQuery] string key, CancellationToken ct)
        {
            return Ok(await progression.PurchaseUpgrade(await CurrentPlayerId(ct), key, ct));
        }

        /// <summary>Refund every spent point and clear all ranks, for an escalating cost.</summary>
        [HttpPost]
        public async Task<ActionResult<PlayerUpgradesDto>> Respec(CancellationToken ct)
        {
            return Ok(await progression.Respec(await CurrentPlayerId(ct), ct));
        }

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
}
