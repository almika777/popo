using NUnit.Framework;
using Popo.Core.Contracts.Iss;
using Popo.Core.PortfolioReturns;

namespace Popo.Tests;

[TestFixture]
public sealed class PortfolioBenchmarkCalculatorTests
{
    [Test]
    public void Calculate_uses_end_of_day_flow_valuation_for_profit()
    {
        var from = new DateOnly(2026, 1, 1);
        var flowDate = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 1, 6);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(flowDate, 210),
            new(to, 210)
        };
        var cashFlows = new[]
        {
            new PortfolioCashFlowInput(flowDate, PortfolioCashFlowType.Deposit, 100)
        };
        var history = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", from, 100m),
            Point("LQDT", new DateOnly(2026, 1, 4), 100m),
            Point("LQDT", flowDate, 110m),
            Point("LQDT", to, 110m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.PortfolioProfitPercent, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.LqdtProfitPercent, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.ExcessReturn, Is.Zero.Within(0.000001));
            Assert.That(result.PortfolioPoints[^1].ProfitPercent,
                Is.EqualTo(result.PortfolioProfitPercent).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_links_portfolio_returns_after_large_deposit()
    {
        var from = new DateOnly(2026, 1, 1);
        var flowDate = new DateOnly(2026, 1, 4);
        var to = new DateOnly(2026, 1, 7);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(new DateOnly(2026, 1, 3), 120),
            new(flowDate, 1120),
            new(to, 1120)
        };
        var cashFlows = new[]
        {
            new PortfolioCashFlowInput(flowDate, PortfolioCashFlowType.Deposit, 1000)
        };
        var history = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", from, 100m),
            Point("LQDT", new DateOnly(2026, 1, 2), 100m),
            Point("LQDT", new DateOnly(2026, 1, 3), 100m),
            Point("LQDT", flowDate, 100m),
            Point("LQDT", new DateOnly(2026, 1, 5), 100m),
            Point("LQDT", new DateOnly(2026, 1, 6), 110m),
            Point("LQDT", to, 110m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.PortfolioProfitPercent, Is.EqualTo(0.2).Within(0.000001));
            Assert.That(result.LqdtProfitPercent, Is.EqualTo(0.1).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_does_not_apply_journal_taxes_to_lqdt_holding_return()
    {
        var from = new DateOnly(2026, 1, 1);
        var taxDate = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 1, 6);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(taxDate, 90),
            new(to, 90)
        };
        var cashFlows = new[]
        {
            new PortfolioCashFlowInput(taxDate, PortfolioCashFlowType.Tax, 10)
        };
        var history = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", new DateOnly(2026, 1, 4), 100m),
            Point("LQDT", taxDate, 100m),
            Point("LQDT", to, 100m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.PortfolioProfitPercent, Is.Zero.Within(0.000001));
            Assert.That(result.LqdtProfitPercent, Is.Zero.Within(0.000001));
        });
    }

    [Test]
    public void Calculate_keeps_paid_bond_coupon_as_cash_without_reinvesting_or_replaying_deposits()
    {
        var from = new DateOnly(2026, 1, 1);
        var depositDate = new DateOnly(2026, 1, 4);
        var to = new DateOnly(2026, 1, 6);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 1000),
            new(depositDate, 1100),
            new(to, 1100)
        };
        var cashFlows = new[]
        {
            new PortfolioCashFlowInput(depositDate, PortfolioCashFlowType.Deposit, 100),
            new PortfolioCashFlowInput(depositDate, PortfolioCashFlowType.Tax, 10)
        };
        var history = BondHistory(
            new PortfolioBondBenchmarkPricePoint(from, 100, null, 1000, 36.5, 0),
            new PortfolioBondBenchmarkPricePoint(new DateOnly(2026, 1, 2), 100, null, 1000, 36.5, 1),
            new PortfolioBondBenchmarkPricePoint(depositDate, 100, null, 1000, 36.5, 1),
            new PortfolioBondBenchmarkPricePoint(new DateOnly(2026, 1, 5), 100, null, 1000, 36.5, 2),
            new PortfolioBondBenchmarkPricePoint(to, 110, null, 1000, 36.5, 3));
        history = history with
        {
            CouponPeriods = new[]
            {
                new PortfolioBondBenchmarkCouponPeriod(new DateOnly(2025, 12, 1), new DateOnly(2026, 1, 3),
                    new DateOnly(2026, 1, 2), 10),
                new PortfolioBondBenchmarkCouponPeriod(new DateOnly(2026, 1, 4), new DateOnly(2026, 2, 3), null, null)
            }
        };
        var lqdtHistory = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", from, 100m),
            Point("LQDT", depositDate, 100m),
            Point("LQDT", new DateOnly(2026, 1, 5), 100m),
            Point("LQDT", to, 100m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, lqdtHistory, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.IndexedBondUnavailableReason, Is.Null);
            Assert.That(result.IndexedBondPoints![^1].ProfitPercent,
                Is.EqualTo(0.113).Within(0.000001));
            Assert.That(result.IndexedBondProfitPercent,
                Is.EqualTo(result.IndexedBondPoints[^1].ProfitPercent).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_late_bond_keeps_capital_in_cash_until_first_quote_and_starts_its_line_there()
    {
        var from = new DateOnly(2025, 12, 30);
        var firstQuoteDate = new DateOnly(2026, 1, 2);
        var to = new DateOnly(2026, 1, 7);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 1000),
            new(to, 1100)
        };
        var cashFlows = Array.Empty<PortfolioCashFlowInput>();
        var bondHistory = BondHistory(
            new PortfolioBondBenchmarkPricePoint(firstQuoteDate, 100, null, 1000, 0, 0),
            new PortfolioBondBenchmarkPricePoint(new DateOnly(2026, 1, 5), 110, null, 1000, 0, 0),
            new PortfolioBondBenchmarkPricePoint(new DateOnly(2026, 1, 6), 110, null, 1000, 0, 0));
        var lqdtHistory = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 29), 100m),
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", firstQuoteDate, 100m),
            Point("LQDT", new DateOnly(2026, 1, 5), 100m),
            Point("LQDT", to, 100m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, lqdtHistory, bondHistory);

        Assert.Multiple(() =>
        {
            Assert.That(result.IndexedBondComparisonStartDate, Is.EqualTo(firstQuoteDate));
            Assert.That(result.IndexedBondPoints![0].Date, Is.EqualTo(firstQuoteDate));
            Assert.That(result.IndexedBondPoints[0].ProfitPercent, Is.EqualTo(0).Within(0.000001));
            Assert.That(result.IndexedBondProfitPercent,
                Is.EqualTo(result.IndexedBondPoints[^1].ProfitPercent).Within(0.000001));
            Assert.That(result.IndexedBondProfitPercent, Is.EqualTo(0.1).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_rebases_all_series_to_first_shared_instrument_date()
    {
        var from = new DateOnly(2026, 1, 1);
        var comparisonStart = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 1, 10);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(comparisonStart, 500),
            new(to, 550)
        };
        var cashFlows = new[]
        {
            new PortfolioCashFlowInput(new DateOnly(2026, 1, 3), PortfolioCashFlowType.Deposit, 100)
        };
        var lqdtHistory = new[]
        {
            Point("LQDT", comparisonStart, 200m),
            Point("LQDT", to, 220m)
        };
        var bondHistory = BondHistory(
            new PortfolioBondBenchmarkPricePoint(new DateOnly(2026, 1, 2), 100, null, 1000, 0, 0),
            new PortfolioBondBenchmarkPricePoint(comparisonStart, 120, null, 1000, 0, 0),
            new PortfolioBondBenchmarkPricePoint(to, 132, null, 1000, 0, 0));

        var result = new PortfolioBenchmarkCalculator().Calculate(
            from, to, valuations, cashFlows, lqdtHistory, bondHistory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ComparisonStartDate, Is.EqualTo(comparisonStart));
            Assert.That(result.PortfolioPoints[0].Date, Is.EqualTo(comparisonStart));
            Assert.That(result.PortfolioProfitPercent, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.LqdtPoints[0].Date, Is.EqualTo(comparisonStart));
            Assert.That(result.LqdtPoints[0].ProfitPercent, Is.Zero.Within(0.000001));
            Assert.That(result.LqdtProfitPercent, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.IndexedBondComparisonStartDate, Is.EqualTo(comparisonStart));
            Assert.That(result.IndexedBondPoints![0].Date, Is.EqualTo(comparisonStart));
            Assert.That(result.IndexedBondPoints[0].ProfitPercent, Is.Zero.Within(0.000001));
            Assert.That(result.IndexedBondProfitPercent, Is.EqualTo(0.1).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_reports_missing_portfolio_valuation_at_shared_start_date()
    {
        var from = new DateOnly(2026, 1, 1);
        var comparisonStart = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 1, 10);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(to, 110)
        };
        var lqdtHistory = new[]
        {
            Point("LQDT", from, 100m),
            Point("LQDT", comparisonStart, 200m),
            Point("LQDT", to, 220m)
        };
        var bondHistory = BondHistory(
            new PortfolioBondBenchmarkPricePoint(comparisonStart, 100, null, 1000, 0, 0),
            new PortfolioBondBenchmarkPricePoint(to, 110, null, 1000, 0, 0));

        var result = new PortfolioBenchmarkCalculator().Calculate(
            from, to, valuations, [], lqdtHistory, bondHistory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ComparisonStartDate, Is.EqualTo(comparisonStart));
            Assert.That(result.PortfolioPoints, Is.Empty);
            Assert.That(result.PortfolioProfitPercent, Is.Null);
            Assert.That(result.PortfolioUnavailableReason, Does.Contain("05.01.2026"));
            Assert.That(result.PortfolioUnavailableReason, Does.Contain("Добавьте оценку"));
            Assert.That(result.LqdtPoints[0].Date, Is.EqualTo(comparisonStart));
            Assert.That(result.LqdtPoints[0].ProfitPercent, Is.Zero.Within(0.000001));
            Assert.That(result.LqdtProfitPercent, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.IndexedBondProfitPercent, Is.EqualTo(0.1).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_does_not_use_unavailable_bond_series_to_choose_shared_start_date()
    {
        var from = new DateOnly(2026, 1, 1);
        var firstBondQuoteDate = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 1, 10);
        var valuations = new PortfolioValuationInput[] { new(from, 100), new(to, 110) };
        var lqdtHistory = new[]
        {
            Point("LQDT", from, 100m),
            Point("LQDT", to, 110m)
        };
        var bondHistory = BondHistory(
            new PortfolioBondBenchmarkPricePoint(firstBondQuoteDate, 100, null, 1000, 0, 0),
            new PortfolioBondBenchmarkPricePoint(to, 110, null, 1000, 0, 0)) with
        {
            FaceUnit = "USD"
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(
            from, to, valuations, [], lqdtHistory, bondHistory);

        Assert.Multiple(() =>
        {
            Assert.That(result.IndexedBondProfitPercent, Is.Null);
            Assert.That(result.ComparisonStartDate, Is.Null);
            Assert.That(result.PortfolioPoints[0].Date, Is.EqualTo(from));
            Assert.That(result.LqdtPoints[0].Date, Is.EqualTo(from));
        });
    }

    [Test]
    public void Calculate_rejects_bond_history_without_actual_accrued_interest_on_trade_dates()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 6);
        var valuations = new PortfolioValuationInput[] { new(from, 1000), new(to, 1000) };
        var bondHistory = BondHistory(
            new PortfolioBondBenchmarkPricePoint(from, 100, null, 1000, 10, null),
            new PortfolioBondBenchmarkPricePoint(new DateOnly(2026, 1, 5), 100, null, 1000, 10, 0)) with
        {
            CouponPeriods = new[]
            {
                new PortfolioBondBenchmarkCouponPeriod(from, new DateOnly(2026, 1, 10), null, null)
            }
        };
        var lqdtHistory = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", new DateOnly(2026, 1, 5), 100m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, [], lqdtHistory, bondHistory);

        Assert.That(result.IndexedBondUnavailableReason, Does.Contain("фактический НКД"));
        Assert.That(result.IndexedBondProfitPercent, Is.Null);
    }

    [Test]
    public void Calculate_marks_bond_unavailable_when_coupon_schedule_cannot_cover_period()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 6);
        var valuations = new PortfolioValuationInput[] { new(from, 1000), new(to, 1000) };
        var bondHistory = BondHistory(
            new PortfolioBondBenchmarkPricePoint(from, 100, null, 1000, 10, 0),
            new PortfolioBondBenchmarkPricePoint(new DateOnly(2026, 1, 5), 100, null, 1000, 10, 0));
        var lqdtHistory = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", to, 100m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, [], lqdtHistory, bondHistory);

        Assert.That(result.IndexedBondUnavailableReason, Does.Contain("Расписание купонов"));
        Assert.That(result.IndexedBondProfitPercent, Is.Null);
    }

    [Test]
    public void Calculate_does_not_assign_coupon_entitlement_recorded_before_benchmark_start()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 6);
        var valuations = new PortfolioValuationInput[] { new(from, 1000), new(to, 1000) };
        var bondHistory = BondHistory(
            new PortfolioBondBenchmarkPricePoint(from, 100, null, 1000, 36.5, 50),
            new PortfolioBondBenchmarkPricePoint(to, 100, null, 1000, 36.5, 1)) with
        {
            CouponPeriods = new[]
            {
                new PortfolioBondBenchmarkCouponPeriod(new DateOnly(2025, 12, 1), new DateOnly(2026, 1, 5),
                    new DateOnly(2025, 12, 30), 10),
                new PortfolioBondBenchmarkCouponPeriod(new DateOnly(2026, 1, 6), new DateOnly(2026, 2, 5), null, null)
            }
        };
        var lqdtHistory = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", from, 100m),
            Point("LQDT", to, 100m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, [], lqdtHistory, bondHistory);

        var expectedValue = 1000d / 1050d * 1001;
        Assert.Multiple(() =>
        {
            Assert.That(result.ComparisonStartDate, Is.EqualTo(from));
            Assert.That(result.IndexedBondPoints![^1].ProfitPercent,
                Is.EqualTo((expectedValue - 1000) / 1000).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_resets_accrued_interest_for_coupon_paid_between_last_quote_and_weekend_start()
    {
        var from = new DateOnly(2026, 1, 4);
        var to = new DateOnly(2026, 1, 7);
        var valuations = new PortfolioValuationInput[] { new(from, 1000), new(to, 1000) };
        var bondHistory = BondHistory(
            new PortfolioBondBenchmarkPricePoint(new DateOnly(2026, 1, 2), 100, null, 1000, 36.5, 2),
            new PortfolioBondBenchmarkPricePoint(new DateOnly(2026, 1, 6), 100, null, 1000, 36.5, 3)) with
        {
            CouponPeriods = new[]
            {
                new PortfolioBondBenchmarkCouponPeriod(new DateOnly(2025, 12, 1), new DateOnly(2026, 1, 3),
                    new DateOnly(2025, 12, 30), 10),
                new PortfolioBondBenchmarkCouponPeriod(new DateOnly(2026, 1, 4), new DateOnly(2026, 2, 3), null, null)
            }
        };
        var lqdtHistory = new[]
        {
            Point("LQDT", new DateOnly(2026, 1, 2), 100m),
            Point("LQDT", new DateOnly(2026, 1, 6), 100m),
            Point("LQDT", to, 100m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, [], lqdtHistory, bondHistory);

        var expectedValue = 1004d / 1003d * 1000;
        Assert.Multiple(() =>
        {
            Assert.That(result.ComparisonStartDate, Is.EqualTo(new DateOnly(2026, 1, 6)));
            Assert.That(result.IndexedBondPoints![^1].ProfitPercent,
                Is.EqualTo((expectedValue - 1000) / 1000).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_lqdt_holding_return_uses_first_and_last_close_inside_period()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(new DateOnly(2026, 1, 4), 110),
            new(new DateOnly(2026, 1, 5), 130),
            new(to, 140)
        };
        var cashFlows = new PortfolioCashFlowInput[]
        {
            new(new DateOnly(2026, 1, 5), PortfolioCashFlowType.Deposit, 20)
        };
        var history = new MoexIndexHistory[]
        {
            Point("LQDT", new DateOnly(2026, 1, 20), 120m),
            Point("OTHER", new DateOnly(2026, 1, 25), 500m),
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", new DateOnly(2026, 1, 4), 110m),
            Point("LQDT", new DateOnly(2026, 1, 5), 110m),
            Point("LQDT", new DateOnly(2026, 1, 6), 120m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(
            from, to, valuations, cashFlows, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.PortfolioReturn, Is.EqualTo(0.18461538461538463).Within(0.000001));
            Assert.That(result.PortfolioPoints.Single(point => point.Date == new DateOnly(2026, 1, 5)).ProfitPercent,
                Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.LqdtReturn, Is.EqualTo(120d / 110 - 1).Within(0.000001));
            Assert.That(result.PortfolioPoints.Select(x => x.Date),
                Is.EqualTo(new[] { from, new DateOnly(2026, 1, 4), new DateOnly(2026, 1, 5), to }));
            Assert.That(result.PortfolioPoints[^1].ReturnPercent,
                Is.EqualTo(result.PortfolioReturn).Within(0.000001));
            Assert.That(result.LqdtStartObservationDate, Is.EqualTo(new DateOnly(2026, 1, 4)));
            Assert.That(result.LqdtEndObservationDate, Is.EqualTo(new DateOnly(2026, 1, 20)));
            Assert.That(result.LqdtPoints.Any(point => point.Date == new DateOnly(2026, 1, 5)), Is.True);
            Assert.That(result.LqdtPoints[^1].ReturnPercent, Is.EqualTo(result.LqdtReturn).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_excludes_tax_outflows_from_portfolio_return()
    {
        var from = new DateOnly(2026, 1, 1);
        var taxDate = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 2, 1);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(new DateOnly(2026, 1, 4), 110),
            new(taxDate, 100),
            new(to, 100)
        };
        var cashFlows = new[]
        {
            new PortfolioCashFlowInput(taxDate, PortfolioCashFlowType.Tax, 10)
        };
        var history = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", new DateOnly(2026, 1, 4), 110m),
            Point("LQDT", new DateOnly(2026, 1, 6), 110m),
            Point("LQDT", new DateOnly(2026, 1, 20), 110m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.PortfolioReturn, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.LqdtReturn, Is.Zero.Within(0.000001));
        });
    }

    [Test]
    public void Calculate_keeps_final_point_when_final_date_has_a_cash_flow()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 5);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(new DateOnly(2026, 1, 4), 110),
            new(to, 100)
        };
        var cashFlows = new[] { new PortfolioCashFlowInput(to, PortfolioCashFlowType.Tax, 10) };
        var history = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", new DateOnly(2026, 1, 4), 110m),
            Point("LQDT", to, 110m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.PortfolioPoints[^1].Date, Is.EqualTo(to));
            Assert.That(result.LqdtPoints[^1].Date, Is.EqualTo(to));
            Assert.That(result.PortfolioPoints[^1].ReturnPercent, Is.EqualTo(result.PortfolioReturn).Within(0.000001));
            Assert.That(result.LqdtPoints[^1].ReturnPercent, Is.EqualTo(result.LqdtReturn).Within(0.000001));
            Assert.That(result.PortfolioReturn, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.LqdtReturn, Is.Zero.Within(0.000001));
        });
    }

    [Test]
    public void Calculate_excludes_tax_cash_outflow_from_portfolio_return()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(new DateOnly(2026, 1, 4), 110),
            new(new DateOnly(2026, 1, 5), 100),
            new(new DateOnly(2026, 1, 9), 100),
            new(new DateOnly(2026, 1, 10), 100),
            new(new DateOnly(2026, 1, 11), 150),
            new(to, 180)
        };
        var cashFlows = new[]
        {
            new PortfolioCashFlowInput(new DateOnly(2026, 1, 5), PortfolioCashFlowType.Tax, 10),
            new PortfolioCashFlowInput(new DateOnly(2026, 1, 11), PortfolioCashFlowType.Deposit, 50)
        };
        var history = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", new DateOnly(2026, 1, 4), 110m),
            Point("LQDT", new DateOnly(2026, 1, 5), 110m),
            Point("LQDT", new DateOnly(2026, 1, 6), 110m),
            Point("LQDT", new DateOnly(2026, 1, 9), 110m),
            Point("LQDT", new DateOnly(2026, 1, 10), 110m),
            Point("LQDT", new DateOnly(2026, 1, 12), 110m),
            Point("LQDT", new DateOnly(2026, 1, 20), 110m),
            Point("LQDT", to, 132m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.PortfolioReturn, Is.EqualTo(0.32).Within(0.000001));
            Assert.That(result.LqdtReturn, Is.EqualTo(0.2).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_aggregates_deposit_withdrawal_and_tax_on_the_same_date()
    {
        var from = new DateOnly(2026, 1, 1);
        var flowDate = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 2, 1);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(new DateOnly(2026, 1, 4), 100),
            new(flowDate, 150),
            new(to, 165)
        };
        var cashFlows = new[]
        {
            new PortfolioCashFlowInput(flowDate, PortfolioCashFlowType.Deposit, 100),
            new PortfolioCashFlowInput(flowDate, PortfolioCashFlowType.Withdrawal, 40),
            new PortfolioCashFlowInput(flowDate, PortfolioCashFlowType.Tax, 10)
        };
        var history = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", new DateOnly(2026, 1, 4), 100m),
            Point("LQDT", new DateOnly(2026, 1, 6), 100m),
            Point("LQDT", new DateOnly(2026, 1, 20), 110m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.PortfolioReturn, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.LqdtReturn, Is.EqualTo(Math.Pow(1.1d, 1d + 12d / 14d) - 1d).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_uses_buy_and_hold_lqdt_return_independently_of_portfolio_weekend_withdrawal()
    {
        var from = new DateOnly(2026, 1, 1);
        var withdrawalDate = new DateOnly(2026, 1, 4);
        var to = new DateOnly(2026, 1, 7);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(withdrawalDate, 50),
            new(to, 50)
        };
        var cashFlows = new[]
        {
            new PortfolioCashFlowInput(withdrawalDate, PortfolioCashFlowType.Withdrawal, 50)
        };
        var history = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", new DateOnly(2026, 1, 2), 100m),
            Point("LQDT", new DateOnly(2026, 1, 5), 200m),
            Point("LQDT", to, 200m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.PortfolioReturn, Is.Zero.Within(0.000001));
            Assert.That(result.LqdtReturn, Is.EqualTo(1).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_lqdt_starts_at_its_first_quote_after_selected_start()
    {
        var from = new DateOnly(2026, 1, 1);
        var firstQuoteDate = new DateOnly(2026, 1, 3);
        var depositDate = new DateOnly(2026, 1, 2);
        var to = new DateOnly(2026, 1, 5);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(depositDate, 150),
            new(to, 165)
        };
        var flows = new[] { new PortfolioCashFlowInput(depositDate, PortfolioCashFlowType.Deposit, 50) };
        var history = new[]
        {
            Point("LQDT", firstQuoteDate, 100m),
            Point("LQDT", to, 110m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, flows, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.LqdtStartObservationDate, Is.EqualTo(firstQuoteDate));
            Assert.That(result.LqdtPoints[0].Date, Is.EqualTo(firstQuoteDate));
            Assert.That(result.LqdtPoints[0].ProfitPercent, Is.Zero.Within(0.000001));
            Assert.That(result.LqdtProfitPercent, Is.EqualTo(0.1).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_lqdt_holding_return_ignores_deposit_on_non_trading_date()
    {
        var from = new DateOnly(2026, 1, 1);
        var depositDate = new DateOnly(2026, 1, 4);
        var to = new DateOnly(2026, 1, 6);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(new DateOnly(2026, 1, 2), 100),
            new(depositDate, 200),
            new(to, 400)
        };
        var flows = new[] { new PortfolioCashFlowInput(depositDate, PortfolioCashFlowType.Deposit, 100) };
        var history = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", new DateOnly(2026, 1, 2), 100m),
            Point("LQDT", to, 200m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, flows, history);

        Assert.That(result.LqdtProfitPercent, Is.EqualTo(1).Within(0.000001));
    }

    [Test]
    public void Calculate_invests_bond_deposit_on_non_trading_date_at_last_close_before_that_date()
    {
        var from = new DateOnly(2026, 1, 1);
        var depositDate = new DateOnly(2026, 1, 4);
        var to = new DateOnly(2026, 1, 6);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 1000),
            new(new DateOnly(2026, 1, 2), 1000),
            new(depositDate, 2000),
            new(to, 4000)
        };
        var flows = new[] { new PortfolioCashFlowInput(depositDate, PortfolioCashFlowType.Deposit, 1000) };
        var lqdtHistory = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", new DateOnly(2026, 1, 2), 100m),
            Point("LQDT", to, 100m)
        };
        var bondHistory = BondHistory(
            new PortfolioBondBenchmarkPricePoint(new DateOnly(2026, 1, 2), 100, null, 1000, 0, 0),
            new PortfolioBondBenchmarkPricePoint(to, 200, null, 1000, 0, 0));

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, flows, lqdtHistory, bondHistory);

        Assert.Multiple(() =>
        {
            Assert.That(result.IndexedBondUnavailableReason, Is.Null);
            Assert.That(result.IndexedBondProfitPercent, Is.EqualTo(1).Within(0.000001));
            Assert.That(result.IndexedBondPoints![^1].ProfitPercent,
                Is.EqualTo(result.IndexedBondProfitPercent).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_ignores_cash_flows_outside_selected_period()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        var valuations = new PortfolioValuationInput[] { new(from, 100), new(to, 110) };
        var cashFlows = new[]
        {
            new PortfolioCashFlowInput(from, PortfolioCashFlowType.Deposit, 50),
            new PortfolioCashFlowInput(new DateOnly(2026, 2, 2), PortfolioCashFlowType.Withdrawal, 20)
        };
        var history = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", new DateOnly(2026, 1, 2), 100m),
            Point("LQDT", new DateOnly(2026, 1, 20), 110m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.PortfolioReturn, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.LqdtReturn, Is.EqualTo(Math.Pow(1.1d, 5d / 3d) - 1d).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_uses_first_close_after_start_and_interpolates_end_between_closes()
    {
        var from = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 2, 1);
        var valuations = new PortfolioValuationInput[] { new(from, 100), new(to, 110) };
        var history = new[]
        {
            Point("LQDT", new DateOnly(2026, 1, 4), 100m),
            Point("LQDT", new DateOnly(2026, 1, 20), 120m),
            Point("LQDT", new DateOnly(2026, 2, 2), 200m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, [], history);

        Assert.Multiple(() =>
        {
            Assert.That(result.LqdtReturn, Is.EqualTo(Math.Pow(200d / 120d, 12d / 13d) - 1d).Within(0.000001));
            Assert.That(result.LqdtEndObservationDate, Is.EqualTo(new DateOnly(2026, 1, 20)));
        });
    }

    [Test]
    public void Calculate_rejects_duplicate_lqdt_prices_for_a_trading_date()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        var valuations = new PortfolioValuationInput[] { new(from, 100), new(to, 110) };
        var history = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", new DateOnly(2026, 1, 20), 110m),
            Point("LQDT", new DateOnly(2026, 1, 20), 111m)
        };

        Assert.Throws<InvalidPortfolioBenchmarkHistoryException>(() =>
            new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, [], history));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Calculate_rejects_non_positive_lqdt_prices(decimal invalidClose)
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        var valuations = new PortfolioValuationInput[] { new(from, 100), new(to, 110) };
        var history = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", new DateOnly(2026, 1, 20), invalidClose)
        };

        Assert.Throws<InvalidPortfolioBenchmarkHistoryException>(() =>
            new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, [], history));
    }

    [Test]
    public void Calculate_ignores_withdrawals_for_lqdt_holding_return()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(new DateOnly(2026, 1, 4), 100),
            new(new DateOnly(2026, 1, 5), 40),
            new(to, 40)
        };
        var cashFlows = new[]
        {
            new PortfolioCashFlowInput(new DateOnly(2026, 1, 5), PortfolioCashFlowType.Withdrawal, 60)
        };
        var history = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", new DateOnly(2026, 1, 4), 50m),
            Point("LQDT", new DateOnly(2026, 1, 6), 50m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, history);

        Assert.That(result.LqdtReturn, Is.Zero.Within(0.000001));
    }

    [Test]
    public void Calculate_ignores_deposits_withdrawals_and_taxes_for_lqdt_holding_return()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 5);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(new DateOnly(2026, 1, 2), 200),
            new(new DateOnly(2026, 1, 3), 180),
            new(new DateOnly(2026, 1, 4), 170),
            new(to, 180)
        };
        var cashFlows = new PortfolioCashFlowInput[]
        {
            new(new DateOnly(2026, 1, 2), PortfolioCashFlowType.Deposit, 100),
            new(new DateOnly(2026, 1, 3), PortfolioCashFlowType.Withdrawal, 10),
            new(new DateOnly(2026, 1, 4), PortfolioCashFlowType.Tax, 10)
        };
        var history = new[]
        {
            Point("LQDT", from, 100m),
            Point("LQDT", new DateOnly(2026, 1, 2), 105m),
            Point("LQDT", new DateOnly(2026, 1, 3), 108m),
            Point("LQDT", to, 120m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.LqdtReturn, Is.EqualTo(0.2).Within(0.000001));
            Assert.That(result.LqdtProfitPercent, Is.EqualTo(0.2).Within(0.000001));
            Assert.That(result.LqdtPoints[0].ProfitPercent, Is.Zero.Within(0.000001));
            Assert.That(result.LqdtPoints[1].ProfitPercent, Is.EqualTo(0.05).Within(0.000001));
            Assert.That(result.LqdtPoints[2].ProfitPercent, Is.EqualTo(0.08).Within(0.000001));
            Assert.That(result.LqdtPoints[3].ProfitPercent,
                Is.EqualTo(Math.Sqrt(108d * 120d) / 100d - 1d).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_returns_lqdt_when_portfolio_cash_flow_valuation_is_missing()
    {
        var from = new DateOnly(2026, 1, 1);
        var flowDate = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 2, 1);
        var valuations = new PortfolioValuationInput[] { new(from, 100), new(to, 110) };
        var cashFlows = new[]
        {
            new PortfolioCashFlowInput(flowDate, PortfolioCashFlowType.Deposit, 10)
        };
        var history = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", new DateOnly(2026, 1, 2), 100m),
            Point("LQDT", new DateOnly(2026, 1, 20), 110m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.LqdtReturn, Is.EqualTo(Math.Pow(1.1d, 5d / 3d) - 1d).Within(0.000001));
            Assert.That(result.LqdtPoints[^1].Date, Is.EqualTo(to));
            Assert.That(result.PortfolioReturn, Is.Null);
            Assert.That(result.PortfolioPoints, Is.Empty);
            Assert.That(result.PortfolioUnavailableReason, Does.Contain("2026-01-05"));
        });
    }

    [Test]
    public void Calculate_starts_lqdt_series_at_first_quote_inside_period()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        var valuations = new PortfolioValuationInput[] { new(from, 100), new(to, 110) };
        var history = new[] { Point("LQDT", new DateOnly(2026, 1, 2), 1.01m) };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, [], history);

        Assert.Multiple(() =>
        {
            Assert.That(result.LqdtStartObservationDate, Is.EqualTo(new DateOnly(2026, 1, 2)));
            Assert.That(result.LqdtPoints[0].Date, Is.EqualTo(new DateOnly(2026, 1, 2)));
            Assert.That(result.LqdtProfitPercent, Is.Zero.Within(0.000001));
            Assert.That(result.ExcessReturn, Is.Null);
        });
    }

    [Test]
    public void Calculate_adds_tmon_to_the_shared_comparison_period()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        var valuations = new PortfolioValuationInput[]
        {
            new(from, 100),
            new(new DateOnly(2026, 1, 2), 100),
            new(to, 110)
        };
        var lqdtHistory = new[]
        {
            Point("LQDT", from, 100m),
            Point("LQDT", to, 120m)
        };
        var tmonHistory = new[]
        {
            Point("TMON", new DateOnly(2026, 1, 2), 100m),
            Point("TMON", to, 110m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(
            from, to, valuations, [], lqdtHistory,
            includeIndexedBond: false,
            tmonPriceHistory: tmonHistory,
            includeTmon: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.ComparisonStartDate, Is.EqualTo(new DateOnly(2026, 1, 2)));
            Assert.That(result.TmonStartObservationDate, Is.EqualTo(new DateOnly(2026, 1, 2)));
            Assert.That(result.TmonProfitPercent, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.TmonPoints![0].ProfitPercent, Is.Zero.Within(0.000001));
            Assert.That(result.TmonPoints![^1].ProfitPercent, Is.EqualTo(0.1).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_ignores_invalid_tmon_history_when_tmon_is_not_selected()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        var valuations = new PortfolioValuationInput[] { new(from, 100), new(to, 110) };
        var history = new[]
        {
            Point("LQDT", from, 100m),
            Point("LQDT", to, 110m),
            Point("TMON", from, 100m),
            Point("TMON", from, 101m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(
            from, to, valuations, [], history, includeIndexedBond: false, includeTmon: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.TmonPoints, Is.Empty);
            Assert.That(result.TmonReturn, Is.Null);
            Assert.That(result.TmonUnavailableReason, Is.Null);
            Assert.That(result.LqdtProfitPercent, Is.EqualTo(0.1).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_throws_when_moex_has_no_trading_price_inside_period()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        var valuations = new PortfolioValuationInput[] { new(from, 100), new(to, 110) };
        var history = new[] { Point("LQDT", new DateOnly(2025, 12, 31), 1m) };

        Assert.Throws<PortfolioBenchmarkHistoryUnavailableException>(() =>
            new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, [], history));
    }

    [Test]
    public void Calculate_returns_minus_100_percent_when_portfolio_value_reaches_zero()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        var valuations = new PortfolioValuationInput[] { new(from, 100), new(to, 0) };
        var history = new[]
        {
            Point("LQDT", new DateOnly(2025, 12, 31), 100m),
            Point("LQDT", new DateOnly(2026, 1, 2), 100m),
            Point("LQDT", new DateOnly(2026, 1, 20), 110m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, [], history);

        Assert.Multiple(() =>
        {
            Assert.That(result.PortfolioReturn, Is.EqualTo(-1).Within(0.000001));
            Assert.That(result.PortfolioPoints[^1].ReturnPercent, Is.EqualTo(-1).Within(0.000001));
            Assert.That(result.LqdtReturn, Is.EqualTo(Math.Pow(1.1d, 5d / 3d) - 1d).Within(0.000001));
        });
    }

    private static PortfolioBondBenchmarkHistory BondHistory(params PortfolioBondBenchmarkPricePoint[] points) =>
        new("RU000A10F504", "TQCB", "RUB", "RUB", points, []);

    private static MoexIndexHistory Point(string secId, DateOnly date, decimal close) => new()
    {
        SecId = secId,
        TradeDate = date,
        Close = close
    };
}
