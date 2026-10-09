using Popo.Core.Contracts.Iss;

namespace Popo.Core.PortfolioReturns;

internal sealed class LqdtBenchmarkSeriesCalculator
{
    private const string SecId = "LQDT";

    public LqdtBenchmarkSeriesCalculation Calculate(
        DateOnly from,
        DateOnly to,
        double startValue,
        IReadOnlyCollection<PortfolioValuationInput> portfolioValuations,
        IReadOnlyCollection<PortfolioCashFlowInput> flows,
        IReadOnlyCollection<MoexIndexHistory> history)
    {
        var prices = history
            .Where(value => string.Equals(value.SecId, SecId, StringComparison.Ordinal))
            .Select(ToPrice)
            .OrderBy(value => value.Date)
            .ToArray();
        var simulation = PortfolioBenchmarkMath.SimulatePriceSeries(
            SecId, from, to, startValue, prices, flows,
            portfolioValuationDates: portfolioValuations.Select(value => value.Date).ToArray());
        var endValue = PortfolioBenchmarkMath.FindValuation(to, simulation.Valuations).TotalValue;
        var profit = PortfolioBenchmarkMath.CalculateProfitPercent(from, to, startValue, endValue, flows);
        var twr = PortfolioBenchmarkMath.CalculateTwrPercent(from, to, simulation.Valuations, flows);
        var lineStartDate = simulation.StartPriceDate > from ? simulation.StartPriceDate : from;
        var points = PortfolioBenchmarkMath.BuildAssetPoints(
            from, to, simulation, portfolioValuations, flows, startValue, lineStartDate);

        return new LqdtBenchmarkSeriesCalculation(
            simulation.StartPriceDate,
            simulation.EndPriceDate,
            new BenchmarkSeriesCalculation(profit, twr, points));
    }

    private static PriceValue ToPrice(MoexIndexHistory value)
    {
        if (value.TradeDate is null || value.Close is null || value.Close <= 0)
        {
            throw new InvalidPortfolioBenchmarkHistoryException("В истории MOEX найдено некорректное значение цены LQDT.");
        }

        return new PriceValue(value.TradeDate.Value, (double)value.Close.Value, true);
    }
}
