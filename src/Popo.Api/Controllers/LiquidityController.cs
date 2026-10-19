using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Api.Services;
using Popo.Core.Portfolio;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/liquidity")]
public sealed class LiquidityController(
    MoneyMarketFundManagementService managementService,
    MoneyMarketFundsPageService fundsPageService) : ControllerBase
{
    /// <summary>Возвращает позиции фондов денежного рынка, доступные фонды для выбора и историю операций.</summary>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Данные страницы фондов денежного рынка.</returns>
    [HttpGet("money-market-funds/page")]
    public async Task<ActionResult<MoneyMarketFundsPageResponse>> GetMoneyMarketFundsPage(
        CancellationToken cancellationToken) =>
        Ok(await fundsPageService.GetAsync(cancellationToken));

    [HttpPost("money-market-funds")]
    public async Task<ActionResult<MoneyMarketFundRecord>> AddMoneyMarketFund(
        [FromBody] UpsertMoneyMarketFundRequest request,
        CancellationToken cancellationToken)
    {
        var record = await managementService.AddMoneyMarketFundAsync(request, cancellationToken);
        return Created($"/api/liquidity/money-market-funds/{record.Id}", record);
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
        return Created($"/api/liquidity/money-market-fund-operations/{record.Id}", record);
    }
}
