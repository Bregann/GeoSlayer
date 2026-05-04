using GeoSlayer.Domain.DTOs.Journey.Requests;
using GeoSlayer.Domain.DTOs.Journey.Responses;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace GeoSlayer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JourneyController(IJourneyService journeyService) : ControllerBase
{
    /// <summary>
    /// Sync the player's position: reveal fog cells, return nearby POIs.
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult<SyncResponse>> Sync(
        [FromBody] SyncRequest request,
        CancellationToken ct)
    {
        var result = await journeyService.Sync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns all revealed cells for a player (used on app startup).
    /// </summary>
    [HttpGet("revealed/{playerId:int}")]
    public async Task<ActionResult<List<CellDto>>> GetRevealed(
        int playerId,
        CancellationToken ct)
    {
        var cells = await journeyService.GetRevealedCells(playerId, ct);
        return Ok(cells);
    }
}
