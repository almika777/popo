using NUnit.Framework;
using Popo.Core.Contracts.Iss;
using Popo.Core.PortfolioReturns;

namespace Popo.Tests;

[TestFixture]
public sealed class PortfolioBenchmarkCalculatorTests
{
    [Test]
    public void Calculate_uses_end_of_day_flow_valuation_for_twr_and_separates_profit_ratio()
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
            Point("LQDT", new DateOnly(2026, 1, 4), 100m),
            Point("LQDT", flowDate, 110m),
            Point("LQDT", to, 110m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.PortfolioProfitPercent, Is.EqualTo(0.05).Within(0.000001));
            Assert.That(result.PortfolioTwrPercent, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.LqdtProfitPercent, Is.EqualTo(0.05).Within(0.000001));
            Assert.That(result.LqdtTwrPercent, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.PortfolioPoints[^1].ProfitPercent,
                Is.EqualTo(result.PortfolioProfitPercent).Within(0.000001));
            Assert.That(result.PortfolioPoints[^1].TwrPercent,
                Is.EqualTo(result.PortfolioTwrPercent).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_reports_money_profit_and_twr_as_different_rankings_for_uneven_capital()
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
            Assert.That(result.PortfolioTwrPercent, Is.EqualTo(0.2).Within(0.000001));
            Assert.That(result.LqdtTwrPercent, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.PortfolioProfitPercent, Is.EqualTo(20d / 1100).Within(0.000001));
            Assert.That(result.LqdtProfitPercent, Is.EqualTo(110d / 1100).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_applies_journal_taxes_to_portfolio_and_benchmark_scenarios()
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
            Assert.That(result.PortfolioProfitPercent, Is.EqualTo(-0.1).Within(0.000001));
            Assert.That(result.PortfolioTwrPercent, Is.EqualTo(-0.1).Within(0.000001));
            Assert.That(result.LqdtProfitPercent, Is.EqualTo(-0.1).Within(0.000001));
            Assert.That(result.LqdtTwrPercent, Is.EqualTo(-0.1).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_keeps_paid_bond_coupon_as_cash_and_does_not_reinvest_it_with_a_later_deposit()
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
        var cashFlows = new[] { new PortfolioCashFlowInput(depositDate, PortfolioCashFlowType.Deposit, 100) };
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

        var expectedValue = (1 + 100d / 1001d) * 1103 + 10;
        Assert.Multiple(() =>
        {
            Assert.That(result.IndexedBondUnavailableReason, Is.Null);
            Assert.That(result.IndexedBondPoints![^1].ProfitPercent,
                Is.EqualTo((expectedValue - 1100) / 1100).Within(0.000001));
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
            Assert.That(result.IndexedBondPoints[0].TwrPercent, Is.EqualTo(0).Within(0.000001));
            Assert.That(result.IndexedBondProfitPercent,
                Is.EqualTo(result.IndexedBondPoints[^1].ProfitPercent).Within(0.000001));
            Assert.That(result.IndexedBondProfitPercent, Is.EqualTo(0.1).Within(0.000001));
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

        Assert.That(result.IndexedBondUnavailableReason, Does.Contain("расписания купонов"));
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
            Point("LQDT", to, 100m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, [], lqdtHistory, bondHistory);

        var expectedValue = 1000d / 1050d * 1001;
        Assert.That(result.IndexedBondPoints![^1].ProfitPercent,
            Is.EqualTo((expectedValue - 1000) / 1000).Within(0.000001));
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
            Point("LQDT", to, 100m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, [], lqdtHistory, bondHistory);

        var expectedValue = 1000d / 1001d * 1004;
        Assert.That(result.IndexedBondPoints![^1].ProfitPercent,
            Is.EqualTo((expectedValue - 1000) / 1000).Within(0.000001));
    }

    [Test]
    public void Calculate_replays_cash_flows_and_matches_portfolio_twr_method()
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
            Assert.That(result.PortfolioReturn, Is.EqualTo(1d / 6).Within(0.000001));
            Assert.That(result.LqdtReturn, Is.EqualTo(2d / 11).Within(0.000001));
            Assert.That(result.PortfolioPoints.Select(x => x.Date),
                Is.EqualTo(new[] { from, new DateOnly(2026, 1, 4), new DateOnly(2026, 1, 5), to }));
            Assert.That(result.PortfolioPoints[^1].ReturnPercent,
                Is.EqualTo(result.PortfolioReturn).Within(0.000001));
            Assert.That(result.LqdtStartObservationDate, Is.EqualTo(new DateOnly(2025, 12, 31)));
            Assert.That(result.LqdtEndObservationDate, Is.EqualTo(new DateOnly(2026, 1, 20)));
            Assert.That(result.LqdtPoints.Any(point => point.Date == new DateOnly(2026, 1, 5)), Is.True);
            Assert.That(result.LqdtPoints[^1].ReturnPercent, Is.EqualTo(result.LqdtReturn).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_replays_tax_on_its_journal_date_in_each_scenario()
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
            Assert.That(result.PortfolioReturn, Is.Zero.Within(0.000001));
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
            Assert.That(result.PortfolioReturn, Is.Zero.Within(0.000001));
            Assert.That(result.LqdtReturn, Is.Zero.Within(0.000001));
        });
    }

    [Test]
    public void Calculate_includes_tax_cash_outflow_in_after_tax_return()
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
            Assert.That(result.PortfolioReturn, Is.EqualTo(30d / 150).Within(0.000001));
            Assert.That(result.LqdtReturn, Is.EqualTo(30d / 150).Within(0.000001));
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
            Assert.That(result.PortfolioReturn, Is.EqualTo(0.025).Within(0.000001));
            Assert.That(result.LqdtReturn, Is.EqualTo(0.025).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_replays_weekend_withdrawal_at_the_last_close_on_or_before_its_date()
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
            Assert.That(result.LqdtReturn, Is.EqualTo(0.5).Within(0.000001));
            Assert.That(result.PortfolioTwrPercent, Is.Zero.Within(0.000001));
            Assert.That(result.LqdtTwrPercent, Is.EqualTo(1).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_lqdt_starts_at_its_first_quote_and_invests_cashflows_accumulated_before_it()
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
            Assert.That(result.LqdtTwrPercent, Is.EqualTo(0.1).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_invests_deposit_on_non_trading_date_at_last_close_before_that_date()
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
            Point("LQDT", new DateOnly(2026, 1, 20), 110m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, history);

        Assert.Multiple(() =>
        {
            Assert.That(result.PortfolioReturn, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(result.LqdtReturn, Is.EqualTo(0.1).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_uses_last_close_on_or_before_dates_and_ignores_future_prices()
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
            Assert.That(result.LqdtReturn, Is.EqualTo(0.2).Within(0.000001));
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
    public void Calculate_rejects_withdrawal_that_the_lqdt_scenario_cannot_fund()
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

        Assert.Throws<PortfolioBenchmarkHistoryUnavailableException>(() =>
            new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, history));
    }

    [Test]
    public void Calculate_requires_the_end_of_day_valuation_for_each_portfolio_cash_flow()
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
            Point("LQDT", new DateOnly(2026, 1, 20), 110m)
        };

        var exception = Assert.Throws<MissingPortfolioBenchmarkValuationsException>(() =>
            new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, cashFlows, history));

        Assert.That(exception!.RequiredDates, Is.EqualTo(new[] { flowDate }));
    }

    [Test]
    public void Calculate_keeps_starting_capital_in_cash_until_first_lqdt_quote()
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
            Point("LQDT", new DateOnly(2026, 1, 20), 110m)
        };

        var result = new PortfolioBenchmarkCalculator().Calculate(from, to, valuations, [], history);

        Assert.Multiple(() =>
        {
            Assert.That(result.PortfolioReturn, Is.EqualTo(-1).Within(0.000001));
            Assert.That(result.PortfolioPoints[^1].ReturnPercent, Is.EqualTo(-1).Within(0.000001));
            Assert.That(result.LqdtReturn, Is.EqualTo(0.1).Within(0.000001));
        });
    }

    private static PortfolioBondBenchmarkHistory BondHistory(params PortfolioBondBenchmarkPricePoint[] points) =>
        new("SU26238RMFS4", "RU000A10F504", "TQOB", "RUB", "RUB", points, []);

    private static MoexIndexHistory Point(string secId, DateOnly date, decimal close) => new()
    {
        SecId = secId,
        TradeDate = date,
        Close = close
    };
}
