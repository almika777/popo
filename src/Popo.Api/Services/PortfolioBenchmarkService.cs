using Popo.Core.Contracts.Iss;
using Popo.Core.Common;
using Popo.Core.HttpClients;
using Popo.Core.PortfolioReturns;

namespace Popo.Api.Services;

public sealed class PortfolioBenchmarkService(
    IPortfolioValuationsProvider valuationsProvider,
    IPortfolioCashFlowsProvider cashFlowsProvider,
    IMoexIndexHistoryStore indexHistoryStore,
    IMoexHttpClient moexHttpClient,
    PortfolioBenchmarkCalculator calculator,
    IPortfolioBondBenchmarkHistoryProvider? bondHistoryProvider = null)
{
    public async Task<PortfolioBenchmarkResult> CalculateAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var valuationsTask = valuationsProvider.GetValuationsAsync(cancellationToken);
        var cashFlowsTask = cashFlowsProvider.GetCashFlowsAsync(cancellationToken);
        var lqdtPriceHistoryTask = indexHistoryStore.GetHistoryAsync(
            "LQDT",
            DateOnly.MinValue,
            DateOnly.MaxValue,
            cancellationToken);

        await Task.WhenAll(valuationsTask, cashFlowsTask, lqdtPriceHistoryTask);
        try
        {
            var lqdtPriceHistory = lqdtPriceHistoryTask.Result;
            var hasStartingPrice = lqdtPriceHistory.Any(value =>
                value.TradeDate <= from && value.Close is > 0);
            var hasPeriodPrice = lqdtPriceHistory.Any(value =>
                value.TradeDate > from && value.TradeDate <= to && value.Close is > 0);
            var historyCoversPeriodEnd = lqdtPriceHistory.Any(value =>
                value.TradeDate >= to && value.Close is > 0);
            if (!hasStartingPrice || !hasPeriodPrice || !historyCoversPeriodEnd)
            {
                var downloadedHistory = (await moexHttpClient.GetLqdtIndexHistoryAsync(cancellationToken))
                    .Where(value => string.Equals(value.SecId, "LQDT", StringComparison.Ordinal))
                    .ToArray();
                if (downloadedHistory.Length == 0)
                {
                    throw new PortfolioBenchmarkHistoryUnavailableException("MOEX не вернул торговые цены LQDT.");
                }

                await indexHistoryStore.UpsertAsync(downloadedHistory, cancellationToken);
                lqdtPriceHistory = downloadedHistory;
            }

            PortfolioBondBenchmarkHistory? indexedBondHistory = null;
            if (bondHistoryProvider is not null)
            {
                indexedBondHistory = await bondHistoryProvider.GetHistoryAsync(
                    "RU000A10F504",
                    from,
                    to,
                    cancellationToken);
            }

            return calculator.Calculate(
                from,
                to,
                valuationsTask.Result.Select(x => new PortfolioValuationInput(x.Date, x.TotalValue)).ToArray(),
                cashFlowsTask.Result.Select(x => new PortfolioCashFlowInput(x.Date, x.Type, x.Amount)).ToArray(),
                lqdtPriceHistory
                    .Where(value => value.TradeDate is not null && value.TradeDate <= to)
                    .ToArray(),
                indexedBondHistory);
        }
        catch (PortfolioBenchmarkHistoryUnavailableException exception)
        {
            throw new InvalidApplicationRequestException(exception.Message, exception);
        }
        catch (MissingPortfolioBenchmarkValuationsException exception)
        {
            throw new InvalidApplicationRequestException(
                $"Для сравнения добавьте оценку портфеля на конец дня после денежных операций: {string.Join(", ", exception.RequiredDates.Select(date => date.ToString("yyyy-MM-dd")))}.",
                exception);
        }
        catch (MissingPortfolioValuationsException exception)
        {
            throw new InvalidApplicationRequestException(
                $"Для сравнения добавьте оценку портфеля на даты: {string.Join(", ", exception.RequiredDates.Select(date => date.ToString("yyyy-MM-dd")))}.",
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
}
