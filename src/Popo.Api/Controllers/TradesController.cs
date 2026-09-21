using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Core.HttpClients;
using Popo.Core.Portfolio;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/trades")]
public sealed class TradesController(
    IPortfolioLedgerProvider ledgerProvider,
    IMoexHttpClient moexHttpClient) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PortfolioTradeRecord>>> GetTrades(
        CancellationToken cancellationToken)
    {
        var trades = await ledgerProvider.GetTradesAsync(cancellationToken);
        if (trades.Count == 0)
        {
            return Ok(trades);
        }

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
        var error = ValidateTrade(request);
        if (error is not null)
        {
            return BadRequest(error);
        }

        var record = await ledgerProvider.AddTradeAsync(ToTrade(request), cancellationToken);
        return Created($"/api/trades/{record.Id}", record);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateTrade(
        Guid id,
        [FromBody] UpsertPortfolioTradeRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateTrade(request);
        if (error is not null)
        {
            return BadRequest(error);
        }

        return await ledgerProvider.UpdateTradeAsync(id, ToTrade(request), cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteTrade(Guid id, CancellationToken cancellationToken) =>
        await ledgerProvider.DeleteTradeAsync(id, cancellationToken) ? NoContent() : NotFound();

    private static PortfolioTrade ToTrade(UpsertPortfolioTradeRequest request) =>
        new(request.SecId.Trim(), request.BoardId.Trim(), request.CurrencyId.Trim(), request.TradeDate,
            request.Side, request.Quantity, request.Price, request.FaceValue,
            request.AccruedInterestTotal / request.Quantity,
            request.CommissionPercent);

    private static string? ValidateTrade(UpsertPortfolioTradeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SecId) || string.IsNullOrWhiteSpace(request.BoardId)
            || string.IsNullOrWhiteSpace(request.CurrencyId))
        {
            return "Укажите бумагу, торговую площадку и валюту сделки.";
        }

        if (!Enum.IsDefined(request.Side))
        {
            return "Тип сделки должен быть: покупка или продажа.";
        }

        return !double.IsFinite(request.Quantity) || request.Quantity <= 0 || request.Quantity != Math.Truncate(request.Quantity)
            ? "Количество должно быть положительным целым числом."
            : !double.IsFinite(request.Price) || request.Price <= 0
                ? "Цена должна быть положительной."
                : !double.IsFinite(request.FaceValue) || request.FaceValue <= 0
                    ? "Номинал должен быть положительным."
                    : !double.IsFinite(request.AccruedInterestTotal) || request.AccruedInterestTotal < 0
                        ? "Общий НКД должен быть неотрицательным."
                        : !double.IsFinite(request.CommissionPercent) || request.CommissionPercent < 0
                            ? "Комиссия должна быть неотрицательной."
                            : null;
    }
}
