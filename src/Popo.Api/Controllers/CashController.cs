using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Api.Services;
using Popo.Core.Portfolio;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/cash")]
public sealed class CashController(
    CashManagementService managementService,
    CashPageService cashPageService,
    MoneyMarketFundsPageService fundsPageService) : ControllerBase
{
    /// <summary>Возвращает снимки, рассчитанные остатки и доступные валюты для страницы кеша.</summary>
    /// <param name="asOf">Дата, на которую рассчитываются остатки.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Данные страницы кеша.</returns>
    [HttpGet("page")]
    public async Task<ActionResult<CashPageResponse>> GetPage(
        [FromQuery] DateOnly asOf,
        CancellationToken cancellationToken) =>
        Ok(await cashPageService.GetAsync(asOf, cancellationToken));

    [HttpGet("money-market-funds/page")]
    public async Task<ActionResult<MoneyMarketFundsPageResponse>> GetMoneyMarketFundsPage(
        CancellationToken cancellationToken) =>
        Ok(await fundsPageService.GetAsync(cancellationToken));

    [HttpPost("snapshots")]
    public async Task<ActionResult<CashSnapshotRecord>> AddCashSnapshot(
        [FromBody] UpsertCashSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        var record = await managementService.AddCashSnapshotAsync(request, cancellationToken);
        return Created($"/api/cash/snapshots/{record.Id}", record);
    }

    [HttpPut("snapshots/{id:guid}")]
    public async Task<ActionResult> UpdateCashSnapshot(
        Guid id,
        [FromBody] UpsertCashSnapshotRequest request,
        CancellationToken cancellationToken)
        => Ok(await managementService.UpdateCashSnapshotAsync(id, request, cancellationToken));

    [HttpDelete("snapshots/{id:guid}")]
    public async Task<ActionResult> DeleteCashSnapshot(Guid id, CancellationToken cancellationToken)
    {
        await managementService.DeleteCashSnapshotAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("money-market-funds")]
    public async Task<ActionResult<MoneyMarketFundRecord>> AddMoneyMarketFund(
        [FromBody] UpsertMoneyMarketFundRequest request,
        CancellationToken cancellationToken)
    {
        var record = await managementService.AddMoneyMarketFundAsync(request, cancellationToken);
        return Created($"/api/cash/money-market-funds/{record.Id}", record);
    }

    [HttpPut("money-market-funds/{id:guid}")]
    public async Task<ActionResult> UpdateMoneyMarketFund(
        Guid id,
        [FromBody] UpsertMoneyMarketFundRequest request,
        CancellationToken cancellationToken)
        => Ok(await managementService.UpdateMoneyMarketFundAsync(id, request, cancellationToken));

    [HttpDelete("money-market-funds/{id:guid}")]
    public async Task<ActionResult> DeleteMoneyMarketFund(Guid id, CancellationToken cancellationToken)
    {
        await managementService.DeleteMoneyMarketFundAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("money-market-fund-operations")]
    public async Task<ActionResult<MoneyMarketFundOperationRecord>> AddMoneyMarketFundOperation(
        [FromBody] AddMoneyMarketFundOperationRequest request,
        CancellationToken cancellationToken)
    {
        var record = await managementService.AddMoneyMarketFundOperationAsync(request, cancellationToken);
        return Created($"/api/cash/money-market-fund-operations/{record.Id}", record);
    }

}
