using Microsoft.AspNetCore.Mvc;
using Popo.Core.Bonds;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/bonds")]
public sealed class BondsController(IBondsService bondsService) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<BondSearchResult>>> Search(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var query = q?.Trim();
        
        if (query is null || query.Length < 3)
            return BadRequest("Введите не менее 3 символов для поиска.");
        
        return Ok(await bondsService.SearchAsync(query, cancellationToken));
    }
}
