using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Api.Services;
using Popo.Core.PortfolioReturns;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/portfolio")]
public sealed class PortfolioController(
    IPortfolioValuationsProvider valuationsProvider,
    IPortfolioCashFlowsProvider cashFlowsProvider,
    PortfolioReturnCalculator calculator,
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
        if ((await valuationsProvider.GetValuationsAsync(cancellationToken)).Any(x => x.Date == request.Date))
        {
            return Conflict($"Оценка портфеля на дату {request.Date:dd.MM.yyyy} уже существует.");
        }

        var record = await valuationsProvider.AddValuationAsync(
            request.Date,
            request.TotalValue,
            request.Comment?.Trim() ?? string.Empty,
            cancellationToken);
        return Created($"/api/portfolio/valuations/{record.Id}", record);
    }

    [HttpPut("valuations/{id:guid}")]
    public async Task<ActionResult> UpdateValuation(
        Guid id,
        [FromBody] UpsertPortfolioValuationRequest request,
        CancellationToken cancellationToken)
    {
        var valuations = await valuationsProvider.GetValuationsAsync(cancellationToken);
        if (valuations.All(x => x.Id != id))
        {
            return NotFound();
        }

        if (valuations.Any(x => x.Id != id && x.Date == request.Date))
        {
            return Conflict($"Оценка портфеля на дату {request.Date:dd.MM.yyyy} уже существует.");
        }

        var updated = await valuationsProvider.UpdateValuationAsync(
            id,
            request.Date,
            request.TotalValue,
            request.Comment?.Trim() ?? string.Empty,
            cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("valuations/{id:guid}")]
    public async Task<ActionResult> DeleteValuation(Guid id, CancellationToken cancellationToken) =>
        await valuationsProvider.DeleteValuationAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("cash-flows")]
    public async Task<ActionResult<PortfolioCashFlowRecord>> AddCashFlow(
        [FromBody] UpsertPortfolioCashFlowRequest request,
        CancellationToken cancellationToken)
    {
        var record = await cashFlowsProvider.AddCashFlowAsync(
            request.Date,
            request.Type,
            request.Amount,
            request.Comment?.Trim() ?? string.Empty,
            cancellationToken);
        return Created($"/api/portfolio/cash-flows/{record.Id}", record);
    }

    [HttpPut("cash-flows/{id:guid}")]
    public async Task<ActionResult> UpdateCashFlow(
        Guid id,
        [FromBody] UpsertPortfolioCashFlowRequest request,
        CancellationToken cancellationToken)
    {
        if (await cashFlowsProvider.GetCashFlowAsync(id, cancellationToken) is null)
        {
            return NotFound();
        }

        var updated = await cashFlowsProvider.UpdateCashFlowAsync(
            id,
            request.Date,
            request.Type,
            request.Amount,
            request.Comment?.Trim() ?? string.Empty,
            cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("cash-flows/{id:guid}")]
    public async Task<ActionResult> DeleteCashFlow(Guid id, CancellationToken cancellationToken) =>
        await cashFlowsProvider.DeleteCashFlowAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("return")]
    public async Task<ActionResult<PortfolioReturnResult>> CalculateReturn(
        [FromQuery] PortfolioReturnQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var valuations = await valuationsProvider.GetValuationsAsync(cancellationToken);
            var cashFlows = await cashFlowsProvider.GetCashFlowsAsync(cancellationToken);
            var result = calculator.Calculate(
                request.From,
                request.To,
                valuations.Select(x => new PortfolioValuationInput(x.Date, x.TotalValue)).ToArray(),
                cashFlows.Select(x => new PortfolioCashFlowInput(x.Date, x.Type, x.Amount)).ToArray());
            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (MissingPortfolioValuationsException exception)
        {
            return BadRequest($"Для TWR добавьте оценку портфеля на даты: {string.Join(", ", exception.RequiredDates.Select(date => date.ToString("yyyy-MM-dd")))}.");
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

}
