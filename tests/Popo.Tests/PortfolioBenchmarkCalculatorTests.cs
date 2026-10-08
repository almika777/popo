using NUnit.Framework;
using Popo.Core.Contracts.Iss;
using Popo.Core.PortfolioReturns;

namespace Popo.Tests;

[TestFixture]
public sealed class PortfolioBenchmarkCalculatorTests
{
    private readonly PortfolioReturnCalculator _returnCalculator = new();

    [Test]
    public void Calculate_uses_twr_for_portfolio_and_latest_moex_values_on_or_before_period_dates()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(new DateOnly(2026, 1, 4), 110),
            new(to, 140)
        };
        var cashFlows = new PortfolioCashFlowInput[]
        {
            new(new DateOnly(2026, 1, 5), PortfolioCashFlowType.Deposit, 20)
        };
        var history = new MoexIndexHistory[]
        {
            Point("LQDTM", new DateOnly(2026, 1, 20), 1.0200m),
            Point("OTHER", new DateOnly(2026, 1, 25), 500m),
            Point("LQDTM", new DateOnly(2025, 12, 31), 1.0000m),
            Point("LQDTM", new DateOnly(2026, 1, 3), 1.0100m)
        };

        var result = new PortfolioBenchmarkCalculator(_returnCalculator).Calculate(
            from, to, valuations, cashFlows, history);

        var benchmark = result.Indexes.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.Portfolio.PeriodReturn, Is.EqualTo(0.1846153846153846).Within(0.000001));
            Assert.That(result.PortfolioPoints.Select(x => x.Date),
                Is.EqualTo(new[] { from, new DateOnly(2026, 1, 4), to }));
            Assert.That(result.PortfolioPoints[^1].IndexedValue,
                Is.EqualTo((1 + result.Portfolio.PeriodReturn) * 100).Within(0.000001));
            Assert.That(benchmark.SecId, Is.EqualTo("LQDTM"));
            Assert.That(benchmark.StartObservationDate, Is.EqualTo(new DateOnly(2025, 12, 31)));
            Assert.That(benchmark.EndObservationDate, Is.EqualTo(new DateOnly(2026, 1, 20)));
            Assert.That(benchmark.PeriodReturn, Is.EqualTo(0.02).Within(0.000001));
            Assert.That(benchmark.Points.Select(x => x.Date),
                Is.EqualTo(new[] { from, new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 20) }));
            Assert.That(benchmark.Points.Select(x => x.IndexedValue),
                Is.EqualTo(new[] { 100d, 101d, 102d }));
        });
    }

    [Test]
    public void Calculate_throws_when_moex_has_no_observation_on_or_before_period_start()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        var valuations = new PortfolioValuationInput[] { new(from, 100), new(to, 110) };
        var history = new[] { Point("LQDTM", new DateOnly(2026, 1, 2), 1.01m) };

        Assert.Throws<PortfolioBenchmarkHistoryUnavailableException>(() =>
            new PortfolioBenchmarkCalculator(_returnCalculator).Calculate(
                from, to, valuations, [], history));
    }

    private static MoexIndexHistory Point(string secId, DateOnly date, decimal close) => new()
    {
        SecId = secId,
        TradeDate = date,
        Close = close
    };
}
