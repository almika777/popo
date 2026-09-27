using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Core.HttpClients;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Trades;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/trades")]
public sealed class TradesController(
    IPortfolioTradesProvider tradesProvider,
    IMoexHttpClient moexHttpClient) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PortfolioTradeRecord>>> GetTrades(
        CancellationToken cancellationToken)
    {
        var trades = await tradesProvider.GetTradesAsync(cancellationToken);
        if (trades.Count == 0)
            return Ok(trades);
        
        var securities = (await moexHttpClient.GetActiveBondsSecuritiesAsync(cancellationToken))
            .SelectMany(x => x.Value)
            .ToArray();

        return Ok(trades.Select(trade => trade with
        {
            ShortName = securities.FirstOrDefault(x => x.SecId == trade.SecId && x.BoardId == trade.BoardId)?.ShortName
                ?? trade.SecId
        }).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<PortfolioTradeRecord>> AddTrade(
        [FromBody] UpsertPortfolioTradeRequest request,
        CancellationToken cancellationToken)
    {
        var record = await tradesProvider.AddTradeAsync(ToTrade(request), cancellationToken);
        return Created($"/api/trades/{record.Id}", record);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateTrade(
        Guid id,
        [FromBody] UpsertPortfolioTradeRequest request,
        CancellationToken cancellationToken)
    {
        return await tradesProvider.UpdateTradeAsync(id, ToTrade(request), cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteTrade(Guid id, CancellationToken cancellationToken) =>
        await tradesProvider.DeleteTradeAsync(id, cancellationToken) ? NoContent() : NotFound();

    private static PortfolioTrade ToTrade(UpsertPortfolioTradeRequest request) =>
        new(request.SecId.Trim(), request.BoardId.Trim(), request.CurrencyId.Trim(), request.TradeDate,
            request.Side, request.Quantity, request.Price, request.FaceValue,
            request.AccruedInterestTotal / request.Quantity,
            request.CommissionPercent);

}
