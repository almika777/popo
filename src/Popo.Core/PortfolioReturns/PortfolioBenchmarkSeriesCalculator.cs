namespace Popo.Core.PortfolioReturns;

internal sealed class PortfolioBenchmarkSeriesCalculator
{
    public BenchmarkSeriesCalculation Calculate(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<PortfolioValuationInput> valuations,
        IReadOnlyCollection<PortfolioCashFlowInput> flows)
    {
        var startValue = PortfolioBenchmarkMath.FindValuation(from, valuations).TotalValue;
        var endValue = PortfolioBenchmarkMath.FindValuation(to, valuations).TotalValue;
        var profit = PortfolioBenchmarkMath.CalculateProfitPercent(from, to, startValue, endValue, flows);
        var points = BuildPoints(from, startValue, valuations, flows);

        return new BenchmarkSeriesCalculation(profit, points);
    }

    private static IReadOnlyList<PortfolioBenchmarkPoint> BuildPoints(
        DateOnly from,
        double startValue,
        IReadOnlyCollection<PortfolioValuationInput> valuations,
        IReadOnlyCollection<PortfolioCashFlowInput> flows)
    {
        var points = new List<PortfolioBenchmarkPoint> { new(from, 0, 0) };
        foreach (var valuation in valuations.Where(value => value.Date > from).OrderBy(value => value.Date))
        {
            var pointFlows = flows.Where(flow => flow.Date <= valuation.Date).ToArray();
            var profit = PortfolioBenchmarkMath.CalculateProfitPercent(
                from, valuation.Date, startValue, valuation.TotalValue, pointFlows);
            points.Add(new PortfolioBenchmarkPoint(valuation.Date, profit, profit));
        }

        return points;
    }
}
