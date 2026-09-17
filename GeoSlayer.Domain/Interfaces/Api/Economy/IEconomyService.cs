using GeoSlayer.Domain.DTOs.Economy.Responses;

namespace GeoSlayer.Domain.Interfaces.Api.Economy
{
    /// <summary>
    /// Coin: selling materials at shops, and banking (DESIGN.md §5.4, §5.4a).
    ///
    /// <para>Selling is a <b>location</b> mechanic, not a menu. Every method that moves coin
    /// validates the player's position against the server's own last verified fix — the
    /// client never supplies one, exactly as with POI visits (§7.2).</para>
    /// </summary>
    public interface IEconomyService
    {
        /// <summary>
        /// Sell materials at a shop POI.
        /// </summary>
        /// <param name="quantityByMaterialId">
        /// What to sell. A quantity above what the player holds sells what they have rather
        /// than failing — a sale is not the place to punish an off-by-one from a stale screen.
        /// </param>
        Task<SellResultDto> Sell(
            int playerId, int poiId, IReadOnlyDictionary<int, long> quantityByMaterialId,
            CancellationToken ct);

        /// <summary>
        /// Sell everything low-tier, in one tap (<c>CoinPricing.IsJunk</c>).
        ///
        /// <para>Exists so the common case is not a chore, and is deliberately conservative:
        /// anything a player might plausibly be saving is left for them to choose.</para>
        /// </summary>
        Task<SellResultDto> SellJunk(int playerId, int poiId, CancellationToken ct);

        /// <summary>The player's coin and deposit, settling any interest owed first.</summary>
        Task<BankAccountDto> GetAccount(int playerId, CancellationToken ct);

        /// <summary>Move coin into the bank. Requires standing at a Banking POI.</summary>
        Task<BankAccountDto> Deposit(int playerId, int poiId, long amount, CancellationToken ct);

        /// <summary>Take coin out. Requires standing at a Banking POI.</summary>
        Task<BankAccountDto> Withdraw(int playerId, int poiId, long amount, CancellationToken ct);
    }
}
