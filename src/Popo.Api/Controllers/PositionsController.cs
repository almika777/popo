using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Api.Services;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/positions")]
public sealed class PositionsController(
    IPortfolioPositionsService positionsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PositionsPageResponse>> GetPositions(
        CancellationToken cancellationToken) =>
        Ok(await positionsService.GetPageAsync(cancellationToken));

}
