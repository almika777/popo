using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Api.Services;
using Popo.Core.PortfolioReturns;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/portfolio")]
public sealed class PortfolioController(
    IPortfolioReturnInputsProvider inputsProvider,
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
        var validationError = ValidateValuation(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        if ((await inputsProvider.GetValuationsAsync(cancellationToken)).Any(x => x.Date == request.Date))
        {
            return Conflict($"Оценка портфеля на дату {request.Date:dd.MM.yyyy} уже существует.");
        }

        var record = await inputsProvider.AddValuationAsync(
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
        var validationError = ValidateValuation(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var valuations = await inputsProvider.GetValuationsAsync(cancellationToken);
        if (valuations.All(x => x.Id != id))
        {
            return NotFound();
        }

        if (valuations.Any(x => x.Id != id && x.Date == request.Date))
        {
            return Conflict($"Оценка портфеля на дату {request.Date:dd.MM.yyyy} уже существует.");
        }

        var updated = await inputsProvider.UpdateValuationAsync(
            id,
            request.Date,
            request.TotalValue,
            request.Comment?.Trim() ?? string.Empty,
            cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("valuations/{id:guid}")]
    public async Task<ActionResult> DeleteValuation(Guid id, CancellationToken cancellationToken) =>
        await inputsProvider.DeleteValuationAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("cash-flows")]
    public async Task<ActionResult<PortfolioCashFlowRecord>> AddCashFlow(
        [FromBody] UpsertPortfolioCashFlowRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateCashFlow(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var record = await inputsProvider.AddCashFlowAsync(
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
        var validationError = ValidateCashFlow(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        if (await inputsProvider.GetCashFlowAsync(id, cancellationToken) is null)
        {
            return NotFound();
        }

        var updated = await inputsProvider.UpdateCashFlowAsync(
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
        await inputsProvider.DeleteCashFlowAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("return")]
    public async Task<ActionResult<PortfolioReturnResult>> CalculateReturn(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        try
        {
            var valuations = await inputsProvider.GetValuationsAsync(cancellationToken);
            var cashFlows = await inputsProvider.GetCashFlowsAsync(cancellationToken);
            var result = calculator.Calculate(
                from,
                to,
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

    private static string? ValidateValuation(UpsertPortfolioValuationRequest request) =>
        !double.IsFinite(request.TotalValue) || request.TotalValue < 0
            ? "Стоимость портфеля должна быть неотрицательной."
            : null;

    private static string? ValidateCashFlow(UpsertPortfolioCashFlowRequest request) =>
        !Enum.IsDefined(request.Type)
            ? "Тип операции должен быть: пополнение, вывод или налог."
            : !double.IsFinite(request.Amount) || request.Amount <= 0
                ? "Сумма должна быть положительной."
                : null;
}
