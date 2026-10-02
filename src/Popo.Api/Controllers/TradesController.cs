using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Api.Services.Trades;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Trades;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/trades")]
public sealed class TradesController(
    PortfolioTradesService tradesService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PortfolioTradeRecord>>> GetTrades(
        CancellationToken cancellationToken)
    {
        return Ok(await tradesService.GetTradesAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<PortfolioTradeRecord>> AddTrade(
        [FromBody] UpsertPortfolioTradeRequest request,
        CancellationToken cancellationToken)
    {
        var record = await tradesService.AddTradeAsync(ToTrade(request), cancellationToken);
        return Created($"/api/trades/{record.Id}", record);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateTrade(
        Guid id,
        [FromBody] UpsertPortfolioTradeRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await tradesService.UpdateTradeAsync(id, ToTrade(request), cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteTrade(Guid id, CancellationToken cancellationToken) =>
        await tradesService.DeleteTradeAsync(id, cancellationToken) ? NoContent() : NotFound();

    private static PortfolioTrade ToTrade(UpsertPortfolioTradeRequest request) =>
        new(request.SecId.Trim(), request.BoardId.Trim(), request.CurrencyId.Trim(), request.TradeDate,
            request.Side, request.Quantity, request.Price, request.FaceValue,
            request.AccruedInterestTotal / request.Quantity,
            request.CommissionPercent);

}
