using Popo.Api.Models;
using Popo.Core.Common;
using Popo.Core.PortfolioReturns;

namespace Popo.Api.Services;

public sealed class PortfolioDataService(
    IPortfolioValuationsProvider valuationsProvider,
    IPortfolioCashFlowsProvider cashFlowsProvider,
    PortfolioReturnCalculator calculator,
    PortfolioBenchmarkService benchmarkService)
{
    public async Task<PortfolioValuationRecord> AddValuationAsync(
        UpsertPortfolioValuationRequest request,
        CancellationToken cancellationToken)
    {
        var valuations = await valuationsProvider.GetValuationsAsync(cancellationToken);
        if (valuations.Any(x => x.Date == request.Date))
        {
            throw new ApplicationConflictException(
                $"Оценка портфеля на дату {request.Date:dd.MM.yyyy} уже существует.");
        }

        return await valuationsProvider.AddValuationAsync(
            request.Date, request.TotalValue, request.Comment?.Trim() ?? string.Empty, cancellationToken);
    }

    public async Task UpdateValuationAsync(
        Guid id,
        UpsertPortfolioValuationRequest request,
        CancellationToken cancellationToken)
    {
        var valuations = await valuationsProvider.GetValuationsAsync(cancellationToken);
        if (valuations.All(x => x.Id != id))
            throw new ApplicationResourceNotFoundException();
        if (valuations.Any(x => x.Id != id && x.Date == request.Date))
            throw new ApplicationConflictException($"Оценка портфеля на дату {request.Date:dd.MM.yyyy} уже существует.");

        if (!await valuationsProvider.UpdateValuationAsync(
                id, request.Date, request.TotalValue, request.Comment?.Trim() ?? string.Empty, cancellationToken))
            throw new ApplicationResourceNotFoundException();
    }

    public async Task DeleteValuationAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await valuationsProvider.DeleteValuationAsync(id, cancellationToken))
            throw new ApplicationResourceNotFoundException();
    }

    public Task<PortfolioCashFlowRecord> AddCashFlowAsync(
        UpsertPortfolioCashFlowRequest request,
        CancellationToken cancellationToken) =>
        cashFlowsProvider.AddCashFlowAsync(
            request.Date, request.Type, request.Amount, request.Comment?.Trim() ?? string.Empty, cancellationToken);

    public async Task UpdateCashFlowAsync(
        Guid id,
        UpsertPortfolioCashFlowRequest request,
        CancellationToken cancellationToken)
    {
        if (!await cashFlowsProvider.UpdateCashFlowAsync(
                id, request.Date, request.Type, request.Amount, request.Comment?.Trim() ?? string.Empty,
                cancellationToken))
            throw new ApplicationResourceNotFoundException();
    }

    public async Task DeleteCashFlowAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await cashFlowsProvider.DeleteCashFlowAsync(id, cancellationToken))
            throw new ApplicationResourceNotFoundException();
    }

    public async Task<PortfolioReturnResult> CalculateReturnAsync(
        PortfolioReturnQuery request,
        CancellationToken cancellationToken)
    {
        var valuations = await valuationsProvider.GetValuationsAsync(cancellationToken);
        var cashFlows = await cashFlowsProvider.GetCashFlowsAsync(cancellationToken);
        try
        {
            return calculator.Calculate(
                request.From,
                request.To,
                valuations.Select(x => new PortfolioValuationInput(x.Date, x.TotalValue)).ToArray(),
                cashFlows.Select(x => new PortfolioCashFlowInput(x.Date, x.Type, x.Amount)).ToArray());
        }
        catch (MissingPortfolioValuationsException exception)
        {
            throw new InvalidApplicationRequestException(
                $"Для TWR добавьте оценку портфеля на даты: {string.Join(", ", exception.RequiredDates.Select(date => date.ToString("yyyy-MM-dd")))}.",
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidApplicationRequestException(exception.Message, exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidApplicationRequestException(exception.Message, exception);
        }
    }

    public async Task<PortfolioBenchmarkResult> CalculateBenchmarkAsync(
        PortfolioReturnQuery request,
        CancellationToken cancellationToken) =>
        await benchmarkService.CalculateAsync(
            request.From,
            request.To,
            cancellationToken,
            request.IncludeLqdt,
            request.IncludeIndexedBond);
}
