using InventoryHold.Contracts;
using InventoryHold.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace InventoryHold.WebApi.Controllers;

[ApiController]
[Route("api/holds")]
public sealed class HoldsController(HoldService service) : ControllerBase
{
    /// <summary>Reserves the requested stock and returns a URL for retrieving the new hold.</summary>
    [HttpPost]
    public async Task<ActionResult<HoldResponse>> Create(CreateHoldRequest request, CancellationToken ct)
    {
        HoldResponse hold = await service.CreateAsync(request, ct);

        return CreatedAtAction(nameof(Get), new { holdId = hold.Id }, hold);
    }

    /// <summary>Retrieves a hold, processing expiration first if its reservation time has elapsed.</summary>
    [HttpGet("{holdId}")]
    public async Task<ActionResult<HoldResponse>> Get(string holdId, CancellationToken ct) =>
        await service.GetAsync(holdId, ct);

    /// <summary>Ends a reservation and restores stock once; repeated releases keep its terminal status.</summary>
    [HttpDelete("{holdId}")]
    public async Task<ActionResult<HoldResponse>> Release(string holdId, CancellationToken ct) =>
        await service.ReleaseAsync(holdId, ct);

    /// <summary>Returns a cursor-paginated list, optionally filtered by reservation status.</summary>
    [HttpGet]
    public async Task<ActionResult<HoldListResponse>> List([FromQuery] HoldStatus? status,
        [FromQuery] string? cursor, CancellationToken ct, [FromQuery] int limit = 50) =>
            await service.ListAsync(status, cursor, limit, ct);
}
