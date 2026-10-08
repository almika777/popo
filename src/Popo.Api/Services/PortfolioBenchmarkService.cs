using Popo.Core.Contracts.Iss;
using Popo.Core.Common;
using Popo.Core.PortfolioReturns;

namespace Popo.Api.Services;

public sealed class PortfolioBenchmarkService(
    IPortfolioValuationsProvider valuationsProvider,
    IPortfolioCashFlowsProvider cashFlowsProvider,
    IMoexIndexHistoryStore indexHistoryStore,
    PortfolioBenchmarkCalculator calculator)
{
    public async Task<PortfolioBenchmarkResult> CalculateAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var valuationsTask = valuationsProvider.GetValuationsAsync(cancellationToken);
        var cashFlowsTask = cashFlowsProvider.GetCashFlowsAsync(cancellationToken);
        var indexHistoryTask = indexHistoryStore.GetHistoryAsync(
            "LQDTM",
            DateOnly.MinValue,
            to,
            cancellationToken);

        await Task.WhenAll(valuationsTask, cashFlowsTask, indexHistoryTask);
        try
        {
            return calculator.Calculate(
                from,
                to,
                valuationsTask.Result.Select(x => new PortfolioValuationInput(x.Date, x.TotalValue)).ToArray(),
                cashFlowsTask.Result.Select(x => new PortfolioCashFlowInput(x.Date, x.Type, x.Amount)).ToArray(),
                indexHistoryTask.Result);
        }
        catch (MissingPortfolioValuationsException exception)
        {
            throw new InvalidApplicationRequestException(
                $"Для TWR добавьте оценку портфеля на даты: {string.Join(", ", exception.RequiredDates.Select(date => date.ToString("yyyy-MM-dd")))}.",
                exception);
        }
        catch (PortfolioBenchmarkHistoryUnavailableException exception)
        {
            throw new InvalidApplicationRequestException(exception.Message, exception);
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
}
