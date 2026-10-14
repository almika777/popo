using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Api.Services;
using Popo.Core.PortfolioReturns;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/portfolio")]
public sealed class PortfolioController(
    PortfolioDataService dataService,
    PortfolioPageService pageService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PortfolioPageResponse>> GetPage(
        [FromQuery] DateOnly asOf,
        CancellationToken cancellationToken) =>
        Ok(await pageService.GetAsync(asOf, cancellationToken));

    [HttpPost("valuations")]
    public async Task<ActionResult<PortfolioValuationRecord>> AddValuation(
        [FromBody] UpsertPortfolioValuationRequest request,
        CancellationToken cancellationToken)
    {
        var record = await dataService.AddValuationAsync(request, cancellationToken);
        return Created($"/api/portfolio/valuations/{record.Id}", record);
    }

    [HttpPut("valuations/{id:guid}")]
    public async Task<ActionResult> UpdateValuation(
        Guid id,
        [FromBody] UpsertPortfolioValuationRequest request,
        CancellationToken cancellationToken)
    {
        await dataService.UpdateValuationAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("valuations/{id:guid}")]
    public async Task<ActionResult> DeleteValuation(Guid id, CancellationToken cancellationToken)
    {
        await dataService.DeleteValuationAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("cash-flows")]
    public async Task<ActionResult<PortfolioCashFlowRecord>> AddCashFlow(
        [FromBody] UpsertPortfolioCashFlowRequest request,
        CancellationToken cancellationToken)
    {
        var record = await dataService.AddCashFlowAsync(request, cancellationToken);
        return Created($"/api/portfolio/cash-flows/{record.Id}", record);
    }

    [HttpPut("cash-flows/{id:guid}")]
    public async Task<ActionResult> UpdateCashFlow(
        Guid id,
        [FromBody] UpsertPortfolioCashFlowRequest request,
        CancellationToken cancellationToken)
    {
        await dataService.UpdateCashFlowAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("cash-flows/{id:guid}")]
    public async Task<ActionResult> DeleteCashFlow(Guid id, CancellationToken cancellationToken)
    {
        await dataService.DeleteCashFlowAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("return")]
    public async Task<ActionResult<PortfolioReturnResult>> CalculateReturn(
        [FromQuery] PortfolioReturnQuery request,
        CancellationToken cancellationToken)
        => Ok(await dataService.CalculateReturnAsync(request, cancellationToken));

    [HttpGet("benchmark")]
    public async Task<ActionResult<PortfolioBenchmarkResult>> CalculateBenchmark(
        [FromQuery] PortfolioReturnQuery request,
        CancellationToken cancellationToken)
        => Ok(await dataService.CalculateBenchmarkAsync(request, cancellationToken));

}
