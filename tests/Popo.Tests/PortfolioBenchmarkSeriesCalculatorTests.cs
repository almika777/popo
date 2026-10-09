using NUnit.Framework;
using Popo.Core.Contracts.Iss;
using Popo.Core.PortfolioReturns;

namespace Popo.Tests;

[TestFixture]
public sealed class PortfolioBenchmarkSeriesCalculatorTests
{
    [Test]
    public void Portfolio_series_calculates_profit_and_twr_independently()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 6);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(new DateOnly(2026, 1, 4), 110),
            new(new DateOnly(2026, 1, 5), 130),
            new(to, 130)
        };
        var flows = new[]
        {
            new PortfolioCashFlowInput(new DateOnly(2026, 1, 5), PortfolioCashFlowType.Deposit, 20)
        };

        var result = new PortfolioBenchmarkSeriesCalculator().Calculate(from, to, valuations, flows);

        Assert.Multiple(() =>
        {
            Assert.That(result.ProfitPercent, Is.EqualTo(1d / 12).Within(0.000001));
            Assert.That(result.TwrPercent, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.Points[^1].ProfitPercent, Is.EqualTo(result.ProfitPercent).Within(0.000001));
            Assert.That(result.Points[^1].TwrPercent, Is.EqualTo(result.TwrPercent).Within(0.000001));
        });
    }

    [Test]
    public void Lqdt_series_uses_its_own_closing_price_history()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 3);
        var valuations = new PortfolioValuationInput[] { new(from, 100), new(to, 100) };
        var history = new[]
        {
            LqdtPoint(from, 1m),
            LqdtPoint(new DateOnly(2026, 1, 2), 1.1m),
            LqdtPoint(to, 1.2m)
        };

        var result = new LqdtBenchmarkSeriesCalculator().Calculate(from, to, 100, valuations, [], history);

        Assert.Multiple(() =>
        {
            Assert.That(result.Series.ProfitPercent, Is.EqualTo(0.2).Within(0.000001));
            Assert.That(result.Series.TwrPercent, Is.EqualTo(0.2).Within(0.000001));
            Assert.That(result.StartPriceDate, Is.EqualTo(from));
            Assert.That(result.EndPriceDate, Is.EqualTo(to));
        });
    }

    [Test]
    public void Lqdt_series_starts_at_first_available_quote_after_period_start()
    {
        var from = new DateOnly(2026, 1, 1);
        var depositDate = new DateOnly(2026, 1, 2);
        var firstQuoteDate = new DateOnly(2026, 1, 3);
        var to = new DateOnly(2026, 1, 5);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(depositDate, 150),
            new(to, 165)
        };
        var flows = new[] { new PortfolioCashFlowInput(depositDate, PortfolioCashFlowType.Deposit, 50) };
        var history = new[] { LqdtPoint(firstQuoteDate, 100m), LqdtPoint(to, 110m) };

        var result = new LqdtBenchmarkSeriesCalculator().Calculate(from, to, 100, valuations, flows, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.StartPriceDate, Is.EqualTo(firstQuoteDate));
            Assert.That(result.Series.Points[0].Date, Is.EqualTo(firstQuoteDate));
            Assert.That(result.Series.Points[0].ProfitPercent, Is.Zero.Within(0.000001));
            Assert.That(result.Series.ProfitPercent, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.Series.TwrPercent, Is.EqualTo(0.1).Within(0.000001));
        });
    }

    [Test]
    public void Indexed_bond_series_uses_its_own_nominal_and_clean_price_history()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 6);
        var valuations = new PortfolioValuationInput[] { new(from, 1000), new(to, 1000) };
        var history = new PortfolioBondBenchmarkHistory(
            "SU000A10F504",
            "RU000A10F504",
            "TQOB",
            "RUB",
            "RUB",
            new[]
            {
                new PortfolioBondBenchmarkPricePoint(from, 100, null, 1000, 0, 0),
                new PortfolioBondBenchmarkPricePoint(new DateOnly(2026, 1, 5), 101, null, 1000, 0, 0)
            },
            []);

        var result = new Ru000A10F504BenchmarkSeriesCalculator().Calculate(
            from, to, 1000, valuations, [], 0, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.UnavailableReason, Is.Null);
            Assert.That(result.Series!.ProfitPercent, Is.EqualTo(0.01).Within(0.000001));
            Assert.That(result.Series.TwrPercent, Is.EqualTo(0.01).Within(0.000001));
            Assert.That(result.ComparisonStartDate, Is.EqualTo(from));
        });
    }

    [Test]
    public void Indexed_bond_series_starts_at_first_quote_without_waiting_for_a_portfolio_valuation()
    {
        var from = new DateOnly(2026, 1, 1);
        var firstQuoteDate = new DateOnly(2026, 1, 3);
        var to = new DateOnly(2026, 1, 6);
        var valuations = new PortfolioValuationInput[] { new(from, 1000), new(to, 1000) };
        var history = new PortfolioBondBenchmarkHistory(
            "SU000A10F504",
            "RU000A10F504",
            "TQOB",
            "RUB",
            "RUB",
            new[]
            {
                new PortfolioBondBenchmarkPricePoint(firstQuoteDate, 100, null, 1000, 0, 0),
                new PortfolioBondBenchmarkPricePoint(to, 110, null, 1000, 0, 0)
            },
            []);

        var result = new Ru000A10F504BenchmarkSeriesCalculator().Calculate(
            from, to, 1000, valuations, [], 0, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.UnavailableReason, Is.Null);
            Assert.That(result.ComparisonStartDate, Is.EqualTo(firstQuoteDate));
            Assert.That(result.Series!.Points[0].Date, Is.EqualTo(firstQuoteDate));
            Assert.That(result.Series.Points[0].ProfitPercent, Is.Zero.Within(0.000001));
            Assert.That(result.Series.ProfitPercent, Is.EqualTo(0.1).Within(0.000001));
        });
    }

    private static MoexIndexHistory LqdtPoint(DateOnly date, decimal close) => new()
    {
        SecId = "LQDT",
        TradeDate = date,
        Close = close
    };
}
