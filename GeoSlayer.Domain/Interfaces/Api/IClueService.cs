using GeoSlayer.Domain.DTOs.Clues.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Interfaces.Api
{
    /// <summary>
    /// Clue scrolls (Stage 13, DESIGN.md §5B) — the directed-travel system.
    /// </summary>
    public interface IClueService
    {
        /// <summary>The player's scrolls, active and completed.</summary>
        Task<List<ClueScrollDto>> GetScrolls(int playerId, CancellationToken ct);

        /// <summary>
        /// Generate a scroll, bounded to the player's own revealed territory (§5B.3:
        /// "always achievable").
        ///
        /// Returns null when the player has too little territory to place steps — better
        /// than an unsolvable scroll.
        /// </summary>
        Task<ClueScrollDto?> GenerateScroll(int playerId, ClueTier tier, CancellationToken ct);

        /// <summary>
        /// Attempt the current step. Arrival is validated <b>server-side</b> against the
        /// player's last verified position, exactly as POI visits are (§7.2) — clue
        /// completion must not become a spoofing vector.
        /// </summary>
        Task<ClueProgressDto> AttemptStep(int playerId, int scrollId, CancellationToken ct);

        /// <summary>One skip per scroll, at a Curation cost (§5B.3).</summary>
        Task<ClueProgressDto> SkipStep(int playerId, int scrollId, CancellationToken ct);
    }
}
