using GeoSlayer.Domain.DTOs.Journey.Requests;
using GeoSlayer.Domain.DTOs.Journey.Responses;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GeoSlayer.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class JourneyController(IJourneyService journeyService) : ControllerBase
{
    /// <summary>
    /// Sync the caller's position: reveal fog cells, return nearby POIs.
    /// The player is resolved from the JWT — never from the request body.
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
    /// Returns all revealed cells for the caller (used on app startup).
    /// </summary>
    [HttpGet("revealed")]
    public async Task<ActionResult<List<CellDto>>> GetRevealed(CancellationToken ct)
    {
        var cells = await journeyService.GetRevealedCells(ct);
        return Ok(cells);
    }
}
