using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Economy.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api.Crafting;
using GeoSlayer.Domain.Interfaces.Api.Economy;
using GeoSlayer.Domain.Interfaces.Api.Materials;
using GeoSlayer.Domain.Interfaces.Api.Progression;
using GeoSlayer.Domain.Services.Fog;
using GeoSlayer.Domain.Services.Idle;
using GeoSlayer.Domain.Services.Progression;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Services.Economy
{
    /// <summary>
    /// Coin (DESIGN.md §5.4, §5.4a) — the economy that resolves §9.5.
    ///
    /// <para><b>Selling is a reason to walk somewhere.</b> Materials are sold at shops, not
    /// from a menu, which is what makes coin a location mechanic in a game about places.
    /// Shops are dense even where terrain is thin, so this asks for a detour rather than
    /// good geography — §4.1a's rule that geography changes how well you play, never
    /// whether you can.</para>
    /// </summary>
    public class EconomyService(
        AppDbContext db,
        IMaterialService materials,
        ICraftingService crafting,
        IProgressionService progression) : IEconomyService
    {
        /// <summary>
        /// Matches <c>SkillTrainingService.PoiInteractRadius</c> and its grace.
        ///
        /// <para>Same numbers on purpose: standing close enough to train at a POI and
        /// standing close enough to trade at it should be the same "close enough", or the
        /// rule becomes something a player has to learn twice.</para>
        /// </summary>
        private const double InteractRadiusMetres = 50;

        /// <summary>Grace for the stored position being one sync stale.</summary>
        private const double RangeGraceMetres = 30;

        // ── Selling (§5.4) ──────────────────────────────────────────────

        public async Task<SellResultDto> Sell(
            int playerId, int poiId, IReadOnlyDictionary<int, long> quantityByMaterialId,
            CancellationToken ct)
        {
            var (player, poi) = await AtPoi(playerId, poiId, EconomySeedData.Shop, ct);

            if (quantityByMaterialId.Count == 0)
            {
                return Empty(player, poi);
            }

            var wanted = quantityByMaterialId.Keys.ToList();

            var rows = await db.PlayerMaterials
                .Include(pm => pm.Material)
                .Where(pm => pm.PlayerId == playerId && wanted.Contains(pm.MaterialId) && pm.Quantity > 0)
                .ToListAsync(ct);

            return await Complete(player, poi, rows, quantityByMaterialId, ct);
        }

        public async Task<SellResultDto> SellJunk(int playerId, int poiId, CancellationToken ct)
        {
            var (player, poi) = await AtPoi(playerId, poiId, EconomySeedData.Shop, ct);

            var held = await db.PlayerMaterials
                .Include(pm => pm.Material)
                .Where(pm => pm.PlayerId == playerId && pm.Quantity > 0)
                .ToListAsync(ct);

            // Filtered here rather than in the query: IsJunk is a rule, and a rule expressed
            // as SQL is one that can drift from the rule expressed in C#.
            var junk = held
                .Where(pm => CoinPricing.IsJunk(pm.Material.Tier, pm.Material.Category))
                .ToList();

            var quantities = junk.ToDictionary(pm => pm.MaterialId, pm => pm.Quantity);

            return await Complete(player, poi, junk, quantities, ct);
        }

        /// <summary>Pays out a sale and removes what was sold.</summary>
        private async Task<SellResultDto> Complete(
            Player player,
            PointOfInterest poi,
            List<PlayerMaterial> rows,
            IReadOnlyDictionary<int, long> requested,
            CancellationToken ct)
        {
            var bonus = await materials.SellPriceBonus(player.Id, ct);

            var sold = new List<SoldMaterialDto>();
            long earned = 0;

            foreach (var row in rows)
            {
                if (!requested.TryGetValue(row.MaterialId, out var wanted) || wanted <= 0)
                {
                    continue;
                }

                // Selling more than you hold sells what you have. A stale inventory screen is
                // the player's normal state on a phone, and failing the whole sale over it
                // would be punishing them for our latency.
                var quantity = Math.Min(wanted, row.Quantity);

                if (quantity <= 0)
                {
                    continue;
                }

                var coin = CoinPricing.StackPrice(
                    row.Material.Tier, row.Material.Category, quantity, bonus);

                row.Quantity -= quantity;
                earned += coin;

                sold.Add(new SoldMaterialDto
                {
                    MaterialId = row.MaterialId,
                    Key = row.Material.Key,
                    Name = row.Material.Name,
                    Quantity = quantity,
                    Coin = coin,
                });
            }

            player.Coin += earned;

            await db.SaveChangesAsync(ct);

            return new SellResultDto
            {
                CoinEarned = earned,
                CoinBalance = player.Coin,
                Sold = sold,
                PoiName = poi.Name,
            };
        }

        private static SellResultDto Empty(Player player, PointOfInterest poi) => new()
        {
            CoinEarned = 0,
            CoinBalance = player.Coin,
            Sold = [],
            PoiName = poi.Name,
        };

        // ── Banking (§5.4a) ─────────────────────────────────────────────

        public async Task<BankAccountDto> GetAccount(int playerId, CancellationToken ct)
        {
            var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct)
                ?? throw new NotFoundException($"Player {playerId} not found.");

            var paid = await SettleInterest(player, ct);

            return await ToDto(player, paid, ct);
        }

        public async Task<BankAccountDto> Deposit(int playerId, int poiId, long amount, CancellationToken ct)
        {
            var (player, _) = await AtPoi(playerId, poiId, EconomySeedData.Bank, ct);

            if (amount <= 0)
            {
                throw new BadRequestException("Deposit something.");
            }

            // Settled before the balance moves, so the new coin cannot earn interest for time
            // it was not deposited.
            var paid = await SettleInterest(player, ct);

            var moved = Math.Min(amount, player.Coin);

            if (moved <= 0)
            {
                throw new BadRequestException("You have no coin to deposit.");
            }

            player.Coin -= moved;
            player.CoinDeposited += moved;

            await db.SaveChangesAsync(ct);

            return await ToDto(player, paid, ct);
        }

        public async Task<BankAccountDto> Withdraw(int playerId, int poiId, long amount, CancellationToken ct)
        {
            var (player, _) = await AtPoi(playerId, poiId, EconomySeedData.Bank, ct);

            if (amount <= 0)
            {
                throw new BadRequestException("Withdraw something.");
            }

            // Settled first so a withdrawal never silently forfeits interest already earned.
            var paid = await SettleInterest(player, ct);

            var moved = Math.Min(amount, player.CoinDeposited);

            if (moved <= 0)
            {
                throw new BadRequestException("You have nothing on deposit.");
            }

            player.CoinDeposited -= moved;
            player.Coin += moved;

            await db.SaveChangesAsync(ct);

            return await ToDto(player, paid, ct);
        }

        /// <summary>
        /// Pays any interest owed since the last settlement, and stamps the clock.
        ///
        /// <para>Always stamps, even when nothing was owed. Leaving the timestamp behind on a
        /// zero balance would let a player bank a long absence, deposit, and immediately
        /// claim interest for time the coin was not in the bank.</para>
        /// </summary>
        private async Task<long> SettleInterest(Player player, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var since = player.InterestSettledUtc;

            player.InterestSettledUtc = now;

            if (since is null || player.CoinDeposited <= 0)
            {
                return 0;
            }

            var capHours = await CapHours(player.Id, ct);
            var interest = BankingInterest.Accrued(player.CoinDeposited, now - since.Value, capHours);

            player.CoinDeposited += interest;

            return interest;
        }

        /// <summary>
        /// Hours of interest one absence earns — the player's offline cap.
        ///
        /// <para>Read from the same upgrade and gear the worker layer reads, so a player who
        /// invested in staying away longer gets that benefit here too rather than having to
        /// learn a second, separate rule.</para>
        /// </summary>
        private async Task<double> CapHours(int playerId, CancellationToken ct)
        {
            var upgrade = await progression.GetUpgradeEffect(
                playerId, ProgressionDefaults.UpgradeKeys.OfflineCap, ct);

            var gear = await crafting.GetModifierTotal(playerId, ItemModifier.OfflineCapHours, ct);

            return OfflineAccrual.BaseOfflineCapHours + upgrade + gear;
        }

        private async Task<BankAccountDto> ToDto(Player player, long justPaid, CancellationToken ct)
        {
            var capHours = await CapHours(player.Id, ct);

            return new BankAccountDto
            {
                Coin = player.Coin,
                Deposited = player.CoinDeposited,
                InterestJustPaid = justPaid,
                InterestPerFullWindow = BankingInterest.PerFullWindow(player.CoinDeposited, capHours),
                CapHours = capHours,
            };
        }

        // ── Position (§7.2) ─────────────────────────────────────────────

        /// <summary>
        /// Loads the player and POI, and refuses unless the player is genuinely there.
        ///
        /// <para>Validated against <c>Player.LastLatitude/Longitude</c> — the server's own
        /// last verified fix. The client never supplies a position to this path; trusting one
        /// would make every shop in the world reachable from an armchair.</para>
        /// </summary>
        private async Task<(Player Player, PointOfInterest Poi)> AtPoi(
            int playerId, int poiId, EconomySeedData.Venue required, CancellationToken ct)
        {
            var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct)
                ?? throw new NotFoundException($"Player {playerId} not found.");

            var poi = await db.PointsOfInterest.FirstOrDefaultAsync(p => p.Id == poiId, ct)
                ?? throw new NotFoundException($"POI {poiId} not found.");

            if (poi.Skill != required.Skill)
            {
                throw new BadRequestException($"{poi.Name} {required.RefusalSuffix}");
            }

            if (player.LastSyncAtUtc is null)
            {
                throw new BadRequestException("No verified position yet — sync first.");
            }

            var distance = TraceValidator.HaversineMetres(
                player.LastLatitude, player.LastLongitude, poi.Location.Y, poi.Location.X);

            // Traveller's Boots and similar extend reach here too (§4.3): an equipped item
            // that works for visiting but not for trading would be a rule with an exception
            // nobody could guess.
            var gearRange = await crafting.GetModifierTotal(playerId, ItemModifier.PoiRangeMetres, ct);

            if (distance > InteractRadiusMetres + RangeGraceMetres + gearRange)
            {
                throw new BadRequestException(
                    $"Too far from {poi.Name} — {Math.Round(distance)}m away.");
            }

            return (player, poi);
        }
    }
}
