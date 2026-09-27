using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Core.Bonds;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/bonds")]
public sealed class BondsController(IBondsService bondsService) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<BondSearchResult>>> Search(
        [FromQuery] BondSearchRequest request,
        CancellationToken cancellationToken)
    {
        var query = request.Query!.Trim();
        return Ok(await bondsService.SearchAsync(query, cancellationToken));
    }
}
