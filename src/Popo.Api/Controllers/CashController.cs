using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Api.Services;
using Popo.Core.Common;
using Popo.Core.Portfolio;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/cash")]
public sealed class CashController(
    IPortfolioCashProvider cashProvider,
    IPortfolioMoneyMarketFundsProvider moneyMarketFundsProvider,
    CashPageService pageService) : ControllerBase
{

    [HttpGet("page")]
    public async Task<ActionResult<CashPageResponse>> GetPage(
        [FromQuery] DateOnly asOf,
        CancellationToken cancellationToken) =>
        Ok(await pageService.GetAsync(asOf, cancellationToken));

    [HttpPost("snapshots")]
    public async Task<ActionResult<CashSnapshotRecord>> AddCashSnapshot(
        [FromBody] UpsertCashSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        var currencyId = request.CurrencyId.Trim();
        if ((await cashProvider.GetCashSnapshotsAsync(cancellationToken))
            .Any(x => x.CurrencyId == currencyId && x.SnapshotDate == request.SnapshotDate))
        {
            return Conflict("Снимок кеша для этой валюты и даты уже существует.");
        }

        var record = await cashProvider.AddCashSnapshotAsync(
            new CashSnapshot(currencyId, request.SnapshotDate, request.Amount),
            request.Comment?.Trim() ?? string.Empty,
            cancellationToken);
        return Created($"/api/cash/snapshots/{record.Id}", record);
    }

    [HttpPut("snapshots/{id:guid}")]
    public async Task<ActionResult> UpdateCashSnapshot(
        Guid id,
        [FromBody] UpsertCashSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        var currencyId = request.CurrencyId.Trim();
        var snapshots = await cashProvider.GetCashSnapshotsAsync(cancellationToken);
        if (snapshots.All(x => x.Id != id))
        {
            return NotFound();
        }

        if (snapshots.Any(x => x.Id != id && x.CurrencyId == currencyId && x.SnapshotDate == request.SnapshotDate))
        {
            return Conflict("Снимок кеша для этой валюты и даты уже существует.");
        }

        var updated = await cashProvider.UpdateCashSnapshotAsync(
            id,
            new CashSnapshot(currencyId, request.SnapshotDate, request.Amount),
            request.Comment?.Trim() ?? string.Empty,
            cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("snapshots/{id:guid}")]
    public async Task<ActionResult> DeleteCashSnapshot(Guid id, CancellationToken cancellationToken) =>
        await cashProvider.DeleteCashSnapshotAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("money-market-funds")]
    public async Task<ActionResult<MoneyMarketFundRecord>> AddMoneyMarketFund(
        [FromBody] UpsertMoneyMarketFundRequest request,
        CancellationToken cancellationToken)
    {
        var definition = RelationHelper.MoneyMarketFunds[request.SecId.Trim()];
        var existing = await moneyMarketFundsProvider.GetMoneyMarketFundsAsync(cancellationToken);
        if (existing.Any(x => string.Equals(x.SecId, request.SecId.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict("Этот фонд денежного рынка уже добавлен.");
        }

        var record = await moneyMarketFundsProvider.AddMoneyMarketFundAsync(
            new MoneyMarketFund(request.SecId.Trim(), definition.BoardId, request.Quantity, request.AveragePrice),
            cancellationToken);
        return Created($"/api/cash/money-market-funds/{record.Id}", record);
    }

    [HttpPut("money-market-funds/{id:guid}")]
    public async Task<ActionResult> UpdateMoneyMarketFund(
        Guid id,
        [FromBody] UpsertMoneyMarketFundRequest request,
        CancellationToken cancellationToken)
    {
        var definition = RelationHelper.MoneyMarketFunds[request.SecId.Trim()];
        var secId = request.SecId.Trim();
        var existing = await moneyMarketFundsProvider.GetMoneyMarketFundsAsync(cancellationToken);
        if (existing.Any(x => x.Id != id && string.Equals(x.SecId, secId, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict("Этот фонд денежного рынка уже добавлен.");
        }

        var updated = await moneyMarketFundsProvider.UpdateMoneyMarketFundAsync(
            id,
            new MoneyMarketFund(secId, definition.BoardId, request.Quantity, request.AveragePrice),
            cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("money-market-funds/{id:guid}")]
    public async Task<ActionResult> DeleteMoneyMarketFund(Guid id, CancellationToken cancellationToken) =>
        await moneyMarketFundsProvider.DeleteMoneyMarketFundAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("money-market-fund-operations")]
    public async Task<ActionResult<MoneyMarketFundOperationRecord>> AddMoneyMarketFundOperation(
        [FromBody] AddMoneyMarketFundOperationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var record = await moneyMarketFundsProvider.AddMoneyMarketFundOperationAsync(
                new MoneyMarketFundOperation(request.SecId!.Trim(), request.Date, request.Side,
                    request.Quantity, request.Price, request.Commission), cancellationToken);
            return Created($"/api/cash/money-market-fund-operations/{record.Id}", record);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }

}
