using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.DTOs.Economy.Requests;
using GeoSlayer.Domain.DTOs.Economy.Responses;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api.Economy;
using GeoSlayer.Domain.Interfaces.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Core.Controllers
{
    /// <summary>Coin: selling and banking (DESIGN.md §5.4, §5.4a).</summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]/[action]")]
    public class EconomyController(
        AppDbContext db,
        IEconomyService economy,
        IUserContextHelper userContextHelper) : ControllerBase
    {
        /// <summary>Sell chosen materials at a shop. Range is checked server-side (§7.2).</summary>
        [HttpPost]
        public async Task<ActionResult<SellResultDto>> Sell(
            [FromBody] SellRequest request, CancellationToken ct) =>
            Ok(await economy.Sell(
                await CurrentPlayerId(ct), request.PoiId, request.Quantities, ct));

        /// <summary>Sell everything low-tier in one go.</summary>
        [HttpPost]
        public async Task<ActionResult<SellResultDto>> SellJunk(
            [FromQuery] int poiId, CancellationToken ct) =>
            Ok(await economy.SellJunk(await CurrentPlayerId(ct), poiId, ct));

        /// <summary>Coin in hand and on deposit, with any interest owed settled first.</summary>
        [HttpGet]
        public async Task<ActionResult<BankAccountDto>> GetAccount(CancellationToken ct) =>
            Ok(await economy.GetAccount(await CurrentPlayerId(ct), ct));

        /// <summary>Deposit coin. Requires standing at a bank.</summary>
        [HttpPost]
        public async Task<ActionResult<BankAccountDto>> Deposit(
            [FromQuery] int poiId, [FromQuery] long amount, CancellationToken ct) =>
            Ok(await economy.Deposit(await CurrentPlayerId(ct), poiId, amount, ct));

        /// <summary>Withdraw coin. Requires standing at a bank.</summary>
        [HttpPost]
        public async Task<ActionResult<BankAccountDto>> Withdraw(
            [FromQuery] int poiId, [FromQuery] long amount, CancellationToken ct) =>
            Ok(await economy.Withdraw(await CurrentPlayerId(ct), poiId, amount, ct));

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
