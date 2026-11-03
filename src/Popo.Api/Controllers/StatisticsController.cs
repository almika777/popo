using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Api.Services.Statistics;
using Popo.Core.Statistics;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/statistics")]
public sealed class StatisticsController(BondDailyStatisticsService statisticsService) : ControllerBase
{
    [HttpGet("bonds/daily-movers")]
    [ProducesResponseType<BondDailyStatisticsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BondDailyStatisticsResult>> GetDailyBondMovers(
        [FromQuery] BondDailyStatisticsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await statisticsService.GetDailyStatisticsAsync(
            request.Date,
            cancellationToken,
            request.MinimumTrades,
            request.MinimumTurnoverRub,
            request.IssuerCategory));
}
