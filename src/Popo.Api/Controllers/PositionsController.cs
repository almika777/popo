using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Api.Services.Position;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/positions")]
public sealed class PositionsController(IOpenPositionsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PositionsPageResponse>> Get(CancellationToken cancellationToken)
        => Ok(await service.GetAsync(cancellationToken));
}
