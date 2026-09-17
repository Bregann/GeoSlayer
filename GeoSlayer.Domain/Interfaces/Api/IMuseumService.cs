using GeoSlayer.Domain.DTOs.Museum.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Interfaces.Api;

/// <summary>
/// The Museum (Stage 12, DESIGN.md §5A).
///
/// <para>Nothing here deletes an entry. Permanence is the design point — the Museum is
/// the only system that never decays, caps or resets — so there is deliberately no
/// removal method to call by accident.</para>
/// </summary>
public interface IMuseumService
{
    /// <summary>Every wing, with found and empty plinths alike.</summary>
    Task<MuseumDto> GetMuseum(int playerId, CancellationToken ct);

    /// <summary>
    /// Record a find. Idempotent on first-find: a repeat increments quantity and leaves
    /// <c>FirstAcquiredUtc</c> alone, so the diary keeps the original date.
    /// </summary>
    Task<MuseumAcquisitionDto?> RecordFind(
        int playerId, string entryKey, int quantity, int? poiId, string? placeName, CancellationToken ct);

    /// <summary>Record entering a region, creating its Cartography plinth on first entry.</summary>
    Task<MuseumAcquisitionDto?> RecordRegion(
        int playerId, double latitude, double longitude, CancellationToken ct);

    /// <summary>Record terrain traversed, materials gathered and any Feats now met.</summary>
    Task<List<MuseumAcquisitionDto>> RecordCellFinds(
        int playerId, IReadOnlyCollection<TerrainType> terrains,
        IReadOnlyCollection<string> materialKeys, CancellationToken ct);

    /// <summary>Re-check Feat thresholds against the player's counters.</summary>
    Task<List<MuseumAcquisitionDto>> CheckFeats(int playerId, CancellationToken ct);

    /// <summary>Donate spares for Curation. The entry itself is never removed.</summary>
    Task<DonationResultDto> DonateDuplicates(
        int playerId, string entryKey, int quantity, CancellationToken ct);
}
