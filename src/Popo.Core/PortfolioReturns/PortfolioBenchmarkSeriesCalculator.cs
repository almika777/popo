using Popo.Core.Portfolio;

namespace Popo.Core.PortfolioReturns;

internal sealed class PortfolioBenchmarkSeriesCalculator
{
    private const double Epsilon = 0.0000001d;

    public BenchmarkSeriesCalculation Calculate(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<PortfolioValuationInput> valuations,
        IReadOnlyCollection<PortfolioCashFlowInput> flows)
    {
        var startValue = PortfolioBenchmarkMath.FindValuation(from, valuations).TotalValue;
        _ = PortfolioBenchmarkMath.FindValuation(to, valuations);
        var points = BuildPoints(from, to, startValue, valuations, flows);

        return new BenchmarkSeriesCalculation(points[^1].ProfitPercent!.Value, points);
    }

    private static IReadOnlyList<PortfolioBenchmarkPoint> BuildPoints(
        DateOnly from,
        DateOnly to,
        double startValue,
        IReadOnlyCollection<PortfolioValuationInput> valuations,
        IReadOnlyCollection<PortfolioCashFlowInput> flows)
    {
        var points = new List<PortfolioBenchmarkPoint> { new(from, 0, 0) };
        var previousValue = startValue;
        var indexValue = 1d;
        var flowsByDate = flows.Where(flow => flow.Date > from && flow.Date <= to)
            .GroupBy(flow => flow.Date)
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach (var valuation in valuations
                     .Where(value => value.Date > from && value.Date <= to)
                     .OrderBy(value => value.Date))
        {
            var dateFlows = flowsByDate.GetValueOrDefault(valuation.Date) ?? [];
            var deposits = dateFlows.Where(flow => flow.Type == PortfolioCashFlowType.Deposit).Sum(flow => flow.Amount);
            var withdrawals = dateFlows.Where(flow => flow.Type == PortfolioCashFlowType.Withdrawal).Sum(flow => flow.Amount);
            var taxes = dateFlows.Where(flow => flow.Type == PortfolioCashFlowType.Tax).Sum(flow => flow.Amount);
            var valueBeforeFlowsAndTaxes = valuation.TotalValue - deposits + withdrawals + taxes;

            if (!double.IsFinite(valueBeforeFlowsAndTaxes) || valueBeforeFlowsAndTaxes < -Epsilon)
            {
                throw new InvalidOperationException("Оценка портфеля не согласуется с денежными потоками на эту дату.");
            }

            valueBeforeFlowsAndTaxes = Math.Max(0, valueBeforeFlowsAndTaxes);
            double periodGrowthFactor;
            if (previousValue <= Epsilon)
            {
                if (valueBeforeFlowsAndTaxes > Epsilon)
                {
                    throw new InvalidOperationException("Нельзя рассчитать доходность после нулевой оценки портфеля.");
                }

                periodGrowthFactor = 1d;
            }
            else
            {
                periodGrowthFactor = valueBeforeFlowsAndTaxes / previousValue;
            }

            indexValue *= periodGrowthFactor;
            if (!double.IsFinite(indexValue))
            {
                throw new InvalidOperationException("Невозможно рассчитать доходность портфеля для указанных оценок.");
            }

            var profit = indexValue - 1d;
            points.Add(new PortfolioBenchmarkPoint(valuation.Date, profit, profit));
            previousValue = valuation.TotalValue;
        }

        return points;
    }
}
