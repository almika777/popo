using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Api.Services;
using Popo.Core.Common;
using Popo.Core.Portfolio;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/cash")]
public sealed class CashController(
    IPortfolioLedgerProvider ledgerProvider,
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
        var error = ValidateCashSnapshot(request);
        if (error is not null)
        {
            return BadRequest(error);
        }

        var currencyId = request.CurrencyId.Trim();
        if ((await ledgerProvider.GetCashSnapshotsAsync(cancellationToken))
            .Any(x => x.CurrencyId == currencyId && x.SnapshotDate == request.SnapshotDate))
        {
            return Conflict("Снимок кеша для этой валюты и даты уже существует.");
        }

        var record = await ledgerProvider.AddCashSnapshotAsync(
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
        var error = ValidateCashSnapshot(request);
        if (error is not null)
        {
            return BadRequest(error);
        }

        var currencyId = request.CurrencyId.Trim();
        var snapshots = await ledgerProvider.GetCashSnapshotsAsync(cancellationToken);
        if (snapshots.All(x => x.Id != id))
        {
            return NotFound();
        }

        if (snapshots.Any(x => x.Id != id && x.CurrencyId == currencyId && x.SnapshotDate == request.SnapshotDate))
        {
            return Conflict("Снимок кеша для этой валюты и даты уже существует.");
        }

        var updated = await ledgerProvider.UpdateCashSnapshotAsync(
            id,
            new CashSnapshot(currencyId, request.SnapshotDate, request.Amount),
            request.Comment?.Trim() ?? string.Empty,
            cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("snapshots/{id:guid}")]
    public async Task<ActionResult> DeleteCashSnapshot(Guid id, CancellationToken cancellationToken) =>
        await ledgerProvider.DeleteCashSnapshotAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("money-market-funds")]
    public async Task<ActionResult<MoneyMarketFundRecord>> AddMoneyMarketFund(
        [FromBody] UpsertMoneyMarketFundRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateMoneyMarketFund(request);
        if (error is not null)
        {
            return BadRequest(error);
        }

        var definition = RelationHelper.MoneyMarketFunds[request.SecId.Trim()];
        var existing = await ledgerProvider.GetMoneyMarketFundsAsync(cancellationToken);
        if (existing.Any(x => string.Equals(x.SecId, request.SecId.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict("Этот фонд денежного рынка уже добавлен.");
        }

        var record = await ledgerProvider.AddMoneyMarketFundAsync(
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
        var error = ValidateMoneyMarketFund(request);
        if (error is not null)
        {
            return BadRequest(error);
        }

        var definition = RelationHelper.MoneyMarketFunds[request.SecId.Trim()];
        var secId = request.SecId.Trim();
        var existing = await ledgerProvider.GetMoneyMarketFundsAsync(cancellationToken);
        if (existing.Any(x => x.Id != id && string.Equals(x.SecId, secId, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict("Этот фонд денежного рынка уже добавлен.");
        }

        var updated = await ledgerProvider.UpdateMoneyMarketFundAsync(
            id,
            new MoneyMarketFund(secId, definition.BoardId, request.Quantity, request.AveragePrice),
            cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("money-market-funds/{id:guid}")]
    public async Task<ActionResult> DeleteMoneyMarketFund(Guid id, CancellationToken cancellationToken) =>
        await ledgerProvider.DeleteMoneyMarketFundAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("money-market-fund-operations")]
    public async Task<ActionResult<MoneyMarketFundOperationRecord>> AddMoneyMarketFundOperation(
        [FromBody] AddMoneyMarketFundOperationRequest request,
        CancellationToken cancellationToken)
    {
        if (!RelationHelper.MoneyMarketFunds.ContainsKey(request.SecId?.Trim() ?? string.Empty)
            || !Enum.IsDefined(request.Side)
            || !double.IsFinite(request.Quantity) || request.Quantity <= 0 || request.Quantity != Math.Truncate(request.Quantity)
            || !double.IsFinite(request.Price) || request.Price <= 0
            || !double.IsFinite(request.Commission) || request.Commission < 0)
        {
            return BadRequest("Проверьте фонд, тип операции, количество, цену и комиссию.");
        }

        try
        {
            var record = await ledgerProvider.AddMoneyMarketFundOperationAsync(
                new MoneyMarketFundOperation(request.SecId!.Trim(), request.Date, request.Side,
                    request.Quantity, request.Price, request.Commission), cancellationToken);
            return Created($"/api/cash/money-market-fund-operations/{record.Id}", record);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }

    private static string? ValidateCashSnapshot(UpsertCashSnapshotRequest request) =>
        string.IsNullOrWhiteSpace(request.CurrencyId)
            ? "Укажите валюту."
            : !double.IsFinite(request.Amount) || request.Amount < 0
                ? "Сумма должна быть неотрицательной."
                : null;

    private static string? ValidateMoneyMarketFund(UpsertMoneyMarketFundRequest request) =>
        !RelationHelper.MoneyMarketFunds.ContainsKey(request.SecId?.Trim() ?? string.Empty)
            ? "Поддерживаются только LQDT и TMON."
            : !double.IsFinite(request.Quantity) || request.Quantity <= 0 || request.Quantity != Math.Truncate(request.Quantity)
                ? "Количество должно быть положительным целым числом."
                : !double.IsFinite(request.AveragePrice) || request.AveragePrice <= 0
                    ? "Средняя цена должна быть положительной."
                    : null;
}
