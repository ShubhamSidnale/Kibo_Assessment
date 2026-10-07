using InventoryHold.Contracts;
using InventoryHold.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace InventoryHold.WebApi.Controllers;

[ApiController]
[Route("api/inventory")]
public sealed class InventoryController(HoldService service) : ControllerBase
{
    /// <summary>Returns available stock using the inventory service's revision-aware cache.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> Get(CancellationToken ct) =>
        Ok(await service.InventoryAsync(ct));
}
