using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;

namespace Popo.Core.PortfolioReturns;

public sealed class PortfolioBenchmarkCalculator
{
    private readonly PortfolioBenchmarkSeriesCalculator _portfolioCalculator = new();
    private readonly LqdtBenchmarkSeriesCalculator _lqdtCalculator = new();
    private readonly Ru000A10F504BenchmarkSeriesCalculator _indexedBondCalculator = new();

    public PortfolioBenchmarkResult Calculate(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<PortfolioValuationInput> valuations,
        IReadOnlyCollection<PortfolioCashFlowInput> cashFlows,
        IReadOnlyCollection<MoexIndexHistory> lqdtPriceHistory,
        PortfolioBondBenchmarkHistory? indexedBondHistory = null)
    {
        ArgumentNullException.ThrowIfNull(valuations);
        ArgumentNullException.ThrowIfNull(cashFlows);
        ArgumentNullException.ThrowIfNull(lqdtPriceHistory);
        if (to <= from)
        {
            throw new ArgumentException("Конечная дата должна быть позже начальной.", nameof(to));
        }

        var periodValuations = GetPeriodValuations(from, to, valuations);
        var startValue = PortfolioBenchmarkMath.FindValuation(from, periodValuations).TotalValue;
        if (startValue <= 0)
        {
            throw new ArgumentException("Стоимость портфеля на начало периода должна быть больше нуля.", nameof(valuations));
        }

        var periodFlows = cashFlows.Where(flow => flow.Date > from && flow.Date <= to).OrderBy(flow => flow.Date).ToArray();
        ValidateCashFlows(periodFlows);
        PortfolioBenchmarkMath.EnsureFlowDateValuations(periodFlows, periodValuations);

        var portfolio = _portfolioCalculator.Calculate(from, to, periodValuations, periodFlows);
        var lqdt = _lqdtCalculator.Calculate(from, to, startValue, periodValuations, periodFlows, lqdtPriceHistory);
        var indexedBond = _indexedBondCalculator.Calculate(
            from, to, startValue, periodValuations, periodFlows,
            portfolio.TwrPercent, indexedBondHistory);

        return new PortfolioBenchmarkResult(
            from, to, portfolio.ProfitPercent, lqdt.Series.ProfitPercent,
            portfolio.ProfitPercent - lqdt.Series.ProfitPercent,
            lqdt.StartPriceDate, lqdt.EndPriceDate,
            portfolio.Points, lqdt.Series.Points,
            indexedBond.Series?.ProfitPercent,
            indexedBond.StartObservationDate,
            indexedBond.EndObservationDate,
            indexedBond.Series?.Points,
            indexedBond.UnavailableReason,
            indexedBond.ComparisonStartDate,
            indexedBond.PortfolioTwrPercent,
            portfolio.ProfitPercent,
            lqdt.Series.ProfitPercent,
            indexedBond.Series?.ProfitPercent,
            portfolio.TwrPercent,
            lqdt.Series.TwrPercent,
            indexedBond.Series?.TwrPercent);
    }

    private static IReadOnlyCollection<PortfolioValuationInput> GetPeriodValuations(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<PortfolioValuationInput> valuations)
    {
        var duplicate = valuations.GroupBy(value => value.Date).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Для даты {duplicate.Key:dd.MM.yyyy} допускается только одна оценка портфеля.");
        }

        if (valuations.Any(value => !double.IsFinite(value.TotalValue) || value.TotalValue < 0))
        {
            throw new ArgumentException("Стоимость портфеля должна быть неотрицательной.", nameof(valuations));
        }

        var period = valuations.Where(value => value.Date >= from && value.Date <= to).OrderBy(value => value.Date).ToArray();
        _ = PortfolioBenchmarkMath.FindValuation(from, period);
        _ = PortfolioBenchmarkMath.FindValuation(to, period);
        return period;
    }

    private static void ValidateCashFlows(IEnumerable<PortfolioCashFlowInput> flows)
    {
        foreach (var flow in flows)
        {
            if (!double.IsFinite(flow.Amount) || flow.Amount <= 0)
            {
                throw new ArgumentException("Сумма денежной операции должна быть положительной.", nameof(flows));
            }
        }
    }
}
