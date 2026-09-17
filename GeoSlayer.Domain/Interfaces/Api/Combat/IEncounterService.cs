using GeoSlayer.Domain.DTOs.Combat.Responses;

namespace GeoSlayer.Domain.Interfaces.Api.Combat
{
    /// <summary>Combat encounters (DESIGN.md §5C).</summary>
    public interface IEncounterService
    {
        /// <summary>
        /// Open encounters near the player, spawning any that are due.
        ///
        /// <para>Spawning happens here rather than on a schedule, on the Surge pattern —
        /// deterministic per cell and window, so it is cheap to run on every call.</para>
        /// </summary>
        Task<List<EncounterDto>> GetEncounters(int playerId, CancellationToken ct);

        /// <summary>Fight one, validated against the server's own last known position.</summary>
        Task<EncounterResultDto> Resolve(int playerId, int encounterId, CancellationToken ct);
    }
}
