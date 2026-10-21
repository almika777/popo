using NUnit.Framework;
using Popo.Core.Contracts.Iss;
using Popo.Core.PortfolioReturns;

namespace Popo.Tests;

[TestFixture]
public sealed class PortfolioBenchmarkSeriesCalculatorTests
{
    [Test]
    public void Portfolio_series_normalizes_all_valuations_to_the_exact_start_value()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 4);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 1_000_000),
            new(new DateOnly(2026, 1, 2), 990_000),
            new(new DateOnly(2026, 1, 3), 1_100_000),
            new(to, 1_200_000)
        };

        var result = new PortfolioBenchmarkSeriesCalculator().Calculate(from, to, valuations, []);

        Assert.That(result.Points.Select(point => point.ProfitPercent),
            Is.EqualTo(new double?[] { 0, -0.01, 0.1, 0.2 }).Within(0.000001));
    }

    [Test]
    public void Portfolio_series_links_returns_after_neutralizing_deposits()
    {
        var from = new DateOnly(2026, 1, 1);
        var depositDate = new DateOnly(2026, 1, 5);
        var growthDate = new DateOnly(2026, 1, 6);
        var to = new DateOnly(2026, 1, 7);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(new DateOnly(2026, 1, 4), 110),
            new(depositDate, 210),
            new(growthDate, 231),
            new(to, 231)
        };
        var flows = new[]
        {
            new PortfolioCashFlowInput(depositDate, PortfolioCashFlowType.Deposit, 100)
        };

        var result = new PortfolioBenchmarkSeriesCalculator().Calculate(from, to, valuations, flows);

        Assert.Multiple(() =>
        {
            Assert.That(result.ProfitPercent, Is.EqualTo(0.21).Within(0.000001));
            Assert.That(result.Points.Single(point => point.Date == depositDate).ProfitPercent,
                Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.Points[^1].ProfitPercent, Is.EqualTo(result.ProfitPercent).Within(0.000001));
        });
    }

    [Test]
    public void Portfolio_series_links_returns_after_neutralizing_withdrawals()
    {
        var from = new DateOnly(2026, 1, 1);
        var withdrawalDate = new DateOnly(2026, 1, 5);
        var growthDate = new DateOnly(2026, 1, 6);
        var to = new DateOnly(2026, 1, 7);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(new DateOnly(2026, 1, 4), 110),
            new(withdrawalDate, 100),
            new(growthDate, 110),
            new(to, 110)
        };
        var flows = new[]
        {
            new PortfolioCashFlowInput(withdrawalDate, PortfolioCashFlowType.Withdrawal, 10)
        };

        var result = new PortfolioBenchmarkSeriesCalculator().Calculate(from, to, valuations, flows);

        Assert.That(result.ProfitPercent, Is.EqualTo(0.21).Within(0.000001));
    }

    [Test]
    public void Portfolio_series_excludes_tax_from_linked_returns()
    {
        var from = new DateOnly(2026, 1, 1);
        var taxDate = new DateOnly(2026, 1, 5);
        var growthDate = new DateOnly(2026, 1, 6);
        var to = new DateOnly(2026, 1, 7);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(new DateOnly(2026, 1, 4), 110),
            new(taxDate, 100),
            new(growthDate, 110),
            new(to, 110)
        };
        var flows = new[]
        {
            new PortfolioCashFlowInput(taxDate, PortfolioCashFlowType.Tax, 10)
        };

        var result = new PortfolioBenchmarkSeriesCalculator().Calculate(from, to, valuations, flows);

        Assert.Multiple(() =>
        {
            Assert.That(result.ProfitPercent, Is.EqualTo(0.21).Within(0.000001));
            Assert.That(result.Points.Single(point => point.Date == taxDate).ProfitPercent,
                Is.EqualTo(0.1).Within(0.000001));
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

        var result = new LqdtBenchmarkSeriesCalculator().Calculate(from, to, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.Series.ProfitPercent, Is.EqualTo(0.2).Within(0.000001));
            Assert.That(result.StartPriceDate, Is.EqualTo(from));
            Assert.That(result.EndPriceDate, Is.EqualTo(to));
        });
    }

    [Test]
    public void Lqdt_series_ignores_lqdtm_index_and_other_security_prices()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 3);
        var history = new[]
        {
            LqdtPoint(from, 100m),
            new MoexIndexHistory { SecId = "LQDTM", TradeDate = from, Close = 1_000m },
            LqdtPoint(to, 125m),
            new MoexIndexHistory { SecId = "OTHER", TradeDate = to, Close = 10_000m }
        };

        var result = new LqdtBenchmarkSeriesCalculator().Calculate(from, to, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.Series.ProfitPercent, Is.EqualTo(0.25).Within(0.000001));
            Assert.That(result.Series.Points.Select(point => point.Date).ToArray(),
                Is.EqualTo(new[] { from, new DateOnly(2026, 1, 2), to }));
            Assert.That(result.Series.Points[1].ProfitPercent,
                Is.EqualTo(Math.Sqrt(1.25d) - 1d).Within(0.000001));
            Assert.That(result.Series.Points[^1].ProfitPercent, Is.EqualTo(0.25).Within(0.000001));
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

        var result = new LqdtBenchmarkSeriesCalculator().Calculate(from, to, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.StartPriceDate, Is.EqualTo(firstQuoteDate));
            Assert.That(result.Series.Points[0].Date, Is.EqualTo(firstQuoteDate));
            Assert.That(result.Series.Points[0].ProfitPercent, Is.Zero.Within(0.000001));
            Assert.That(result.Series.ProfitPercent, Is.EqualTo(0.1).Within(0.000001));
        });
    }

    [Test]
    public void Lqdt_series_uses_fixed_holding_return_from_first_to_last_quote()
    {
        var from = new DateOnly(2026, 1, 1);
        var firstQuoteDate = new DateOnly(2026, 1, 2);
        var middleQuoteDate = new DateOnly(2026, 1, 3);
        var to = new DateOnly(2026, 1, 4);
        var history = new[]
        {
            LqdtPoint(firstQuoteDate, 100m),
            LqdtPoint(middleQuoteDate, 110m),
            LqdtPoint(to, 121m)
        };

        var result = new LqdtBenchmarkSeriesCalculator().Calculate(from, to, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.Series.ProfitPercent, Is.EqualTo(0.21).Within(0.000001));
            Assert.That(result.Series.Points.Select(point => point.Date),
                Is.EqualTo(new[] { firstQuoteDate, middleQuoteDate, to }));
            Assert.That(result.Series.Points[0].ProfitPercent, Is.Zero.Within(0.000001));
            Assert.That(result.Series.Points[1].ProfitPercent, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.Series.Points[2].ProfitPercent, Is.EqualTo(0.21).Within(0.000001));
        });
    }

    [Test]
    public void Lqdt_series_calculates_exact_hold_return_from_official_daily_close_prices()
    {
        var from = new DateOnly(2026, 9, 9);
        var lastTradingDate = new DateOnly(2026, 9, 11);
        var to = lastTradingDate;
        var history = new[]
        {
            LqdtPoint(from, 2.0811m),
            LqdtPoint(lastTradingDate, 2.0840m)
        };

        var result = new LqdtBenchmarkSeriesCalculator().Calculate(from, to, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.Series.ProfitPercent, Is.EqualTo(0.001393493825380808).Within(0.000000000000001));
            Assert.That(result.StartPriceDate, Is.EqualTo(from));
            Assert.That(result.EndPriceDate, Is.EqualTo(lastTradingDate));
            Assert.That(result.Series.Points[^1].Date, Is.EqualTo(to));
            Assert.That(result.Series.Points[^1].ProfitPercent, Is.EqualTo(result.Series.ProfitPercent).Within(0.000000000000001));
        });
    }

    [Test]
    public void Lqdt_series_interpolates_weekend_return_from_next_trading_quote()
    {
        var from = new DateOnly(2026, 9, 11);
        var to = new DateOnly(2026, 9, 13);
        var nextTradingDate = new DateOnly(2026, 9, 14);
        var history = new[]
        {
            LqdtPoint(from, 100m),
            LqdtPoint(nextTradingDate, 103.0301m)
        };

        var result = new LqdtBenchmarkSeriesCalculator().Calculate(from, to, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.Series.ProfitPercent, Is.EqualTo(0.0201).Within(0.000000001));
            Assert.That(result.StartPriceDate, Is.EqualTo(from));
            Assert.That(result.EndPriceDate, Is.EqualTo(from));
            Assert.That(result.Series.Points.Select(point => point.Date).ToArray(),
                Is.EqualTo(new[] { from, new DateOnly(2026, 9, 12), to }));
            Assert.That(result.Series.Points.Select(point => point.ProfitPercent).ToArray(),
                Is.EqualTo(new double?[] { 0d, 0.01d, 0.0201d }).Within(0.000000001));
        });
    }

    [Test]
    public void Lqdt_series_extrapolates_weekend_from_last_observed_daily_factor()
    {
        var previousTradingDate = new DateOnly(2026, 9, 10);
        var from = new DateOnly(2026, 9, 11);
        var to = new DateOnly(2026, 9, 13);
        var history = new[]
        {
            LqdtPoint(previousTradingDate, 100m),
            LqdtPoint(from, 101m)
        };

        var result = new LqdtBenchmarkSeriesCalculator().Calculate(from, to, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.Series.ProfitPercent, Is.EqualTo(0.0201).Within(0.000000001));
            Assert.That(result.EndPriceDate, Is.EqualTo(from));
            Assert.That(result.Series.Points.Select(point => point.Date).ToArray(),
                Is.EqualTo(new[] { from, new DateOnly(2026, 9, 12), to }));
            Assert.That(result.Series.Points.Select(point => point.ProfitPercent).ToArray(),
                Is.EqualTo(new double?[] { 0d, 0.01d, 0.0201d }).Within(0.000000001));
        });
    }

    [Test]
    public void Lqdt_series_sorts_quotes_before_selecting_period_boundaries()
    {
        var from = new DateOnly(2026, 1, 1);
        var startQuoteDate = new DateOnly(2026, 1, 2);
        var middleQuoteDate = new DateOnly(2026, 1, 3);
        var to = new DateOnly(2026, 1, 4);
        var history = new[]
        {
            LqdtPoint(middleQuoteDate, 110m),
            LqdtPoint(to, 121m),
            LqdtPoint(startQuoteDate, 100m)
        };

        var result = new LqdtBenchmarkSeriesCalculator().Calculate(from, to, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.StartPriceDate, Is.EqualTo(startQuoteDate));
            Assert.That(result.EndPriceDate, Is.EqualTo(to));
            Assert.That(result.Series.ProfitPercent, Is.EqualTo(0.21).Within(0.000001));
            Assert.That(result.Series.Points.Select(point => point.Date),
                Is.EqualTo(new[] { startQuoteDate, middleQuoteDate, to }));
        });
    }

    [Test]
    public void Lqdt_series_ignores_quote_before_period_start()
    {
        var from = new DateOnly(2026, 1, 2);
        var firstQuoteDate = new DateOnly(2026, 1, 3);
        var to = new DateOnly(2026, 1, 5);
        var history = new[]
        {
            LqdtPoint(new DateOnly(2026, 1, 1), 50m),
            LqdtPoint(firstQuoteDate, 100m),
            LqdtPoint(new DateOnly(2026, 1, 4), 110m),
            LqdtPoint(to, 121m)
        };

        var result = new LqdtBenchmarkSeriesCalculator().Calculate(from, to, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.StartPriceDate, Is.EqualTo(firstQuoteDate));
            Assert.That(result.Series.Points[0].Date, Is.EqualTo(firstQuoteDate));
            Assert.That(result.Series.ProfitPercent, Is.EqualTo(0.21).Within(0.000001));
        });
    }

    [Test]
    public void Lqdt_series_uses_the_next_quote_to_interpolate_the_end_and_ignores_later_quotes()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 5);
        var lastQuoteDate = new DateOnly(2026, 1, 4);
        var history = new[]
        {
            LqdtPoint(new DateOnly(2026, 1, 2), 100m),
            LqdtPoint(new DateOnly(2026, 1, 3), 110m),
            LqdtPoint(lastQuoteDate, 120m),
            LqdtPoint(new DateOnly(2026, 1, 6), 121m),
            LqdtPoint(new DateOnly(2026, 1, 7), 500m)
        };

        var result = new LqdtBenchmarkSeriesCalculator().Calculate(from, to, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.EndPriceDate, Is.EqualTo(lastQuoteDate));
            var expectedReturn = Math.Sqrt(120d * 121d) / 100d - 1d;
            Assert.That(result.Series.ProfitPercent, Is.EqualTo(expectedReturn).Within(0.000001));
            Assert.That(result.Series.Points[^1].Date, Is.EqualTo(to));
            Assert.That(result.Series.Points[^1].ProfitPercent, Is.EqualTo(expectedReturn).Within(0.000001));
        });
    }

    [Test]
    public void Lqdt_series_returns_zero_for_a_single_quote_in_period()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 4);
        var quoteDate = new DateOnly(2026, 1, 2);

        var result = new LqdtBenchmarkSeriesCalculator().Calculate(
            from, to, [LqdtPoint(quoteDate, 100m)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Series.ProfitPercent, Is.Zero.Within(0.000001));
            Assert.That(result.Series.Points[0].Date, Is.EqualTo(quoteDate));
            Assert.That(result.Series.Points[^1].Date, Is.EqualTo(to));
        });
    }

    [Test]
    public void Lqdt_series_returns_zero_for_a_single_day_period_with_one_quote()
    {
        var tradeDate = new DateOnly(2026, 1, 2);
        var price = 2.0811m;

        var result = new LqdtBenchmarkSeriesCalculator().Calculate(
            tradeDate, tradeDate, [LqdtPoint(tradeDate, price)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.StartPriceDate, Is.EqualTo(tradeDate));
            Assert.That(result.EndPriceDate, Is.EqualTo(tradeDate));
            Assert.That(result.Series.ProfitPercent, Is.Zero);
            Assert.That(result.Series.Points, Has.Count.EqualTo(1));
            Assert.That(result.Series.Points[0].Date, Is.EqualTo(tradeDate));
            Assert.That(result.Series.Points[0].ProfitPercent, Is.Zero);
        });
    }

    [Test]
    public void Lqdt_series_rejects_a_period_ending_before_it_starts()
    {
        var from = new DateOnly(2026, 1, 3);
        var to = new DateOnly(2026, 1, 2);

        var exception = Assert.Throws<ArgumentException>(() =>
            new LqdtBenchmarkSeriesCalculator().Calculate(from, to, []));

        Assert.That(exception!.ParamName, Is.EqualTo("to"));
    }

    [Test]
    public void Lqdt_series_calculates_negative_holding_return_from_first_and_last_period_quotes()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 5);

        var result = new LqdtBenchmarkSeriesCalculator().Calculate(from, to,
        [
            LqdtPoint(from, 100m),
            LqdtPoint(new DateOnly(2026, 1, 3), 90m),
            LqdtPoint(to, 80m)
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Series.ProfitPercent, Is.EqualTo(-0.2).Within(0.000001));
            Assert.That(result.Series.Points[^1].ProfitPercent, Is.EqualTo(-0.2).Within(0.000001));
        });
    }

    [Test]
    public void Lqdt_series_ignores_malformed_prices_outside_selected_period()
    {
        var from = new DateOnly(2026, 1, 2);
        var to = new DateOnly(2026, 1, 5);

        var result = new LqdtBenchmarkSeriesCalculator().Calculate(from, to,
        [
            LqdtPoint(new DateOnly(2026, 1, 1), 0m),
            LqdtPoint(from, 100m),
            LqdtPoint(to, 110m),
            LqdtPoint(new DateOnly(2026, 1, 6), 0m)
        ]);

        Assert.That(result.Series.ProfitPercent, Is.EqualTo(0.1).Within(0.000001));
    }

    [Test]
    public void Lqdt_series_is_unavailable_when_there_are_no_quotes_in_period()
    {
        var from = new DateOnly(2026, 1, 2);
        var to = new DateOnly(2026, 1, 4);
        var history = new[]
        {
            LqdtPoint(new DateOnly(2026, 1, 1), 100m),
            LqdtPoint(new DateOnly(2026, 1, 5), 110m)
        };

        Assert.Throws<PortfolioBenchmarkHistoryUnavailableException>(() =>
            new LqdtBenchmarkSeriesCalculator().Calculate(from, to, history));
    }

    [Test]
    public void Lqdt_series_rejects_null_history()
    {
        var date = new DateOnly(2026, 1, 2);

        Assert.Throws<ArgumentNullException>(() =>
            new LqdtBenchmarkSeriesCalculator().Calculate(date, date, null!));
    }

    [Test]
    public void Indexed_bond_series_uses_its_own_nominal_and_clean_price_history()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 6);
        var history = new PortfolioBondBenchmarkHistory(
            "RU000A10F504",
            "TQCB",
            "RUB",
            "RUB",
            new[]
            {
                new PortfolioBondBenchmarkPricePoint(from, 100, null, 1000, 0, 0),
                new PortfolioBondBenchmarkPricePoint(new DateOnly(2026, 1, 5), 101, null, 1000, 0, 0)
            },
            []);

        var result = new Ru000A10F504BenchmarkSeriesCalculator().Calculate(from, to, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.UnavailableReason, Is.Null);
            Assert.That(result.Series!.ProfitPercent, Is.EqualTo(0.01).Within(0.000001));
            Assert.That(result.ComparisonStartDate, Is.EqualTo(from));
        });
    }

    [Test]
    public void Indexed_bond_series_starts_at_first_quote_without_waiting_for_a_portfolio_valuation()
    {
        var from = new DateOnly(2026, 1, 1);
        var firstQuoteDate = new DateOnly(2026, 1, 3);
        var to = new DateOnly(2026, 1, 6);
        var history = new PortfolioBondBenchmarkHistory(
            "RU000A10F504",
            "TQCB",
            "RUB",
            "RUB",
            new[]
            {
                new PortfolioBondBenchmarkPricePoint(firstQuoteDate, 100, null, 1000, 0, 0),
                new PortfolioBondBenchmarkPricePoint(to, 110, null, 1000, 0, 0)
            },
            []);

        var result = new Ru000A10F504BenchmarkSeriesCalculator().Calculate(from, to, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.UnavailableReason, Is.Null);
            Assert.That(result.ComparisonStartDate, Is.EqualTo(firstQuoteDate));
            Assert.That(result.Series!.Points[0].Date, Is.EqualTo(firstQuoteDate));
            Assert.That(result.Series.Points[0].ProfitPercent, Is.Zero.Within(0.000001));
            Assert.That(result.Series.ProfitPercent, Is.EqualTo(0.1).Within(0.000001));
        });
    }

    [Test]
    public void Indexed_bond_series_ignores_a_pre_period_quote_and_buys_at_first_quote_in_period()
    {
        var from = new DateOnly(2026, 1, 2);
        var firstQuoteDate = new DateOnly(2026, 1, 3);
        var to = new DateOnly(2026, 1, 6);
        var history = new PortfolioBondBenchmarkHistory(
            "RU000A10F504",
            "TQCB",
            "RUB",
            "RUB",
            new[]
            {
                new PortfolioBondBenchmarkPricePoint(new DateOnly(2026, 1, 1), 50, null, 1000, 0, 0),
                new PortfolioBondBenchmarkPricePoint(firstQuoteDate, 100, null, 1000, 0, 0),
                new PortfolioBondBenchmarkPricePoint(to, 110, null, 1000, 0, 0)
            },
            []);

        var result = new Ru000A10F504BenchmarkSeriesCalculator().Calculate(from, to, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.UnavailableReason, Is.Null);
            Assert.That(result.ComparisonStartDate, Is.EqualTo(firstQuoteDate));
            Assert.That(result.StartObservationDate, Is.EqualTo(firstQuoteDate));
            Assert.That(result.Series!.Points[0].Date, Is.EqualTo(firstQuoteDate));
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
