using Popo.Core.Contracts.Iss;

namespace Popo.Core.PortfolioReturns;

public sealed class PortfolioBenchmarkCalculator(PortfolioReturnCalculator portfolioReturnCalculator)
{
    private const string LqdtIndexSecId = "LQDTM";
    private const string LqdtIndexName = "LQDT (расчётная стоимость пая)";
    private const double InitialIndexedValue = 100d;

    public PortfolioBenchmarkResult Calculate(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<PortfolioValuationInput> valuations,
        IReadOnlyCollection<PortfolioCashFlowInput> cashFlows,
        IReadOnlyCollection<MoexIndexHistory> indexHistory)
    {
        var portfolioReturn = portfolioReturnCalculator.Calculate(from, to, valuations, cashFlows);
        var portfolioPoints = CalculatePortfolioPoints(from, to, valuations, cashFlows);
        var lqdtBenchmark = CalculateLqdtBenchmark(from, to, portfolioReturn.PeriodReturn, indexHistory);

        return new PortfolioBenchmarkResult(
            from,
            to,
            portfolioReturn,
            portfolioPoints,
            [lqdtBenchmark]);
    }

    private IReadOnlyList<PortfolioBenchmarkPoint> CalculatePortfolioPoints(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<PortfolioValuationInput> valuations,
        IReadOnlyCollection<PortfolioCashFlowInput> cashFlows)
    {
        var points = new List<PortfolioBenchmarkPoint> { new(from, InitialIndexedValue) };
        foreach (var valuation in valuations
                     .Where(value => value.Date > from && value.Date <= to)
                     .OrderBy(value => value.Date))
        {
            var cumulativeReturn = portfolioReturnCalculator
                .Calculate(from, valuation.Date, valuations, cashFlows)
                .PeriodReturn;
            points.Add(new PortfolioBenchmarkPoint(
                valuation.Date,
                InitialIndexedValue * (1 + cumulativeReturn)));
        }

        return points;
    }

    private static PortfolioIndexBenchmark CalculateLqdtBenchmark(
        DateOnly from,
        DateOnly to,
        double portfolioReturn,
        IReadOnlyCollection<MoexIndexHistory> indexHistory)
    {
        var values = indexHistory
            .Where(value => value.SecId == LqdtIndexSecId)
            .Select(ToIndexValue)
            .OrderBy(value => value.Date)
            .ToArray();

        var duplicateDate = values
            .GroupBy(value => value.Date)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateDate is not null)
        {
            throw new InvalidPortfolioBenchmarkHistoryException(
                $"В истории MOEX несколько значений LQDTM за {duplicateDate.Key:yyyy-MM-dd}.");
        }

        var start = values.LastOrDefault(value => value.Date <= from)
            ?? throw new PortfolioBenchmarkHistoryUnavailableException(
                $"Нет значения LQDTM на дату {from:yyyy-MM-dd} или раньше.");
        var periodValues = values.Where(value => value.Date > from && value.Date <= to).ToArray();
        var end = periodValues.LastOrDefault() ?? start;
        var periodReturn = (double)(end.Value / start.Value - 1m);
        var points = new List<PortfolioBenchmarkPoint> { new(from, InitialIndexedValue) };
        points.AddRange(periodValues.Select(value => new PortfolioBenchmarkPoint(
            value.Date,
            (double)(value.Value / start.Value * (decimal)InitialIndexedValue))));

        return new PortfolioIndexBenchmark(
            LqdtIndexSecId,
            LqdtIndexName,
            start.Date,
            end.Date,
            start.Value,
            end.Value,
            periodReturn,
            portfolioReturn - periodReturn,
            points);
    }

    private static IndexValue ToIndexValue(MoexIndexHistory value)
    {
        if (value.TradeDate is null || value.Close is null || value.Close <= 0)
        {
            throw new InvalidPortfolioBenchmarkHistoryException("В истории MOEX найдено некорректное значение LQDTM.");
        }

        return new IndexValue(value.TradeDate.Value, value.Close.Value);
    }

    private sealed record IndexValue(DateOnly Date, decimal Value);
}
