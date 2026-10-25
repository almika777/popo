using NUnit.Framework;
using Popo.Core.Extensions;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.ClosedSessions;

namespace Popo.Tests;

[TestFixture]
public sealed class ClosedPositionSessionCalculatorTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);

    [TestCaseSource(nameof(SessionCalculationScenarios))]
    public void Calculate_PopulatesEveryClosedSessionField(ClosedSessionInstrument instrument,
        DateTimeOffset calculatedAt, ClosedPositionSession expected)
    {
        var result = new ClosedPositionSessionCalculator().Calculate(instrument, calculatedAt).Single();

        Assert.That(result, Is.EqualTo(expected));
    }

    private static IEnumerable<TestCaseData> SessionCalculationScenarios()
    {
        var calculatedAt = new DateTimeOffset(2026, 2, 1, 9, 30, 0, TimeSpan.FromHours(3));
        var annualStart = new DateOnly(2025, 1, 1);
        var annualEnd = new DateOnly(2026, 1, 1);
        var annualBuyId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        yield return new TestCaseData(
            ScenarioInstrument("RU000A000001",
                [FixedTrade(annualBuyId, annualStart, TradeSide.Buy, 1, 1000, 25),
                 FixedTrade(Guid.Parse("00000000-0000-0000-0000-000000000002"), annualEnd,
                     TradeSide.Sell, 1, 1100, 25)],
                [FixedCoupon(annualStart, annualEnd, 365)]),
            calculatedAt,
            new ClosedPositionSession(annualBuyId, "RU000A000001", "TQCB", "RUB", "RU000A000001 bond",
                annualStart, annualEnd, 365, 1, 1000, 1100, 50, 0, 100, 365, 415, 41.5d,
                (415d * 365 / (1025d * 365) * 100).Round(),
                true, null, "Default", calculatedAt))
            .SetName("Complete_session_all_fields_annual_holding");

        var partialStart = new DateOnly(2026, 1, 1);
        var partialEnd = partialStart.AddDays(5);
        var partialBuyId = Guid.Parse("00000000-0000-0000-0000-000000000011");
        yield return new TestCaseData(
            ScenarioInstrument("RU000A000011",
                [FixedTrade(partialBuyId, partialStart, TradeSide.Buy, 4, 100, 1),
                 FixedTrade(Guid.Parse("00000000-0000-0000-0000-000000000012"), partialStart.AddDays(2),
                     TradeSide.Sell, 1, 110, 0.25d),
                 FixedTrade(Guid.Parse("00000000-0000-0000-0000-000000000013"), partialStart.AddDays(2),
                     TradeSide.Buy, 1, 100, 0.75d),
                 FixedTrade(Guid.Parse("00000000-0000-0000-0000-000000000014"), partialEnd,
                     TradeSide.Sell, 4, 110, 1)],
                [FixedCoupon(partialStart, partialEnd, 5)]),
            calculatedAt,
            new ClosedPositionSession(partialBuyId, "RU000A000011", "TQCB", "RUB", "RU000A000011 bond",
                partialStart, partialEnd, 5, 5, 500, 550, 3, 0, 50, 20, 67, 13.4d,
                (67d * 365 / 2006.5d * 100).Round(),
                true, null, "Default", calculatedAt))
            .SetName("Complete_session_all_fields_partial_sale_and_rebuy");

        var amortStart = new DateOnly(2026, 1, 1);
        var amortEnd = amortStart.AddDays(25);
        var amortBuyId = Guid.Parse("00000000-0000-0000-0000-000000000021");
        yield return new TestCaseData(
            ScenarioInstrument("RU000A000021",
                [FixedTrade(amortBuyId, amortStart, TradeSide.Buy, 2, 1000),
                 FixedTrade(Guid.Parse("00000000-0000-0000-0000-000000000022"), amortEnd,
                     TradeSide.Sell, 2, 900)],
                [FixedCoupon(amortStart, amortEnd, 25)],
                [FixedAmortization(amortStart.AddDays(10), 100)], 1000),
            calculatedAt,
            new ClosedPositionSession(amortBuyId, "RU000A000021", "TQCB", "RUB", "RU000A000021 bond",
                amortStart, amortEnd, 25, 2, 2000, 1800, 0, 200, 0, 50, 50, 2.5d, 36.5d,
                true, null, "Default", calculatedAt))
            .SetName("Complete_session_all_fields_partial_amortization");

        var incompleteStart = new DateOnly(2026, 1, 1);
        var incompleteEnd = incompleteStart.AddDays(2);
        var incompleteCouponBuyId = Guid.Parse("00000000-0000-0000-0000-000000000031");
        yield return new TestCaseData(
            ScenarioInstrument("RU000A000031",
                [FixedTrade(incompleteCouponBuyId, incompleteStart, TradeSide.Buy, 1, 100, 2),
                 FixedTrade(Guid.Parse("00000000-0000-0000-0000-000000000032"), incompleteEnd,
                     TradeSide.Sell, 1, 110, 3)],
                [FixedCoupon(incompleteStart, incompleteStart.AddDays(1), 1)]),
            calculatedAt,
            new ClosedPositionSession(incompleteCouponBuyId, "RU000A000031", "TQCB", "RUB",
                "RU000A000031 bond", incompleteStart, incompleteEnd, 2, 1, 100, 110, 5,
                0, 10, null, null, null, null, false,
                "Купонный график отсутствует или неоднозначен на 2026-01-02.", "Default", calculatedAt))
            .SetName("Incomplete_session_all_fields_missing_coupon_period");

        var incompleteAmortBuyId = Guid.Parse("00000000-0000-0000-0000-000000000041");
        yield return new TestCaseData(
            ScenarioInstrument("RU000A000041",
                [FixedTrade(incompleteAmortBuyId, incompleteStart, TradeSide.Buy, 1, 1000, 1),
                 FixedTrade(Guid.Parse("00000000-0000-0000-0000-000000000042"), incompleteEnd,
                     TradeSide.Sell, 1, 1000, 2)],
                [FixedCoupon(incompleteStart, incompleteEnd, 2)],
                [new ClosedSessionAmortization(incompleteStart.AddDays(1), null, null, null, 1000, "USD")]),
            calculatedAt,
            new ClosedPositionSession(incompleteAmortBuyId, "RU000A000041", "TQCB", "RUB",
                "RU000A000041 bond", incompleteStart, incompleteEnd, 2, 1, 1000, 1000, 3,
                null, null, 2, null, null, null, false,
                "Недостаточно данных для отделения частичной амортизации от погашения.", "Default", calculatedAt))
            .SetName("Incomplete_session_all_fields_unknown_amortization");

        var sameDayBuyId = Guid.Parse("00000000-0000-0000-0000-000000000051");
        yield return new TestCaseData(
            ScenarioInstrument("RU000A000051",
                [FixedTrade(Guid.Parse("00000000-0000-0000-0000-000000000050"), incompleteStart,
                     TradeSide.Sell, 1, 110, 1),
                 FixedTrade(sameDayBuyId, incompleteStart, TradeSide.Buy, 1, 100, 1)], []),
            calculatedAt,
            new ClosedPositionSession(sameDayBuyId, "RU000A000051", "TQCB", "RUB", "RU000A000051 bond",
                incompleteStart, incompleteStart, 0, 1, 100, 110, 2, 0, 10, 0, 8, 8, null,
                true, null, "Default", calculatedAt))
            .SetName("Complete_session_all_fields_same_day_round_trip");
    }

    [Test]
    public void Calculate_PartialSalesReduceCouponIncomeFromSaleDate()
    {
        var result = Calculate([
            Trade(0, TradeSide.Buy, 10, 100, 2),
            Trade(20, TradeSide.Sell, 4, 110, 1),
            Trade(50, TradeSide.Sell, 6, 110, 1)
        ], [Coupon(0, 60, 60)]).Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.CouponIncome, Is.EqualTo(380d));
            Assert.That(result.BodyPnl, Is.EqualTo(100d));
            Assert.That(result.TotalPnl, Is.EqualTo(476d));
            Assert.That(result.TotalPnlPercent, Is.EqualTo(47.6d).Within(0.000001));
            Assert.That(result.AnnualizedPnlPercent, Is.EqualTo(456.3d));
        });
    }

    [Test]
    public void Calculate_AnnualizesAgainstCapitalDaysForEachPurchase()
    {
        var result = Calculate([
            Trade(0, TradeSide.Buy, 2, 100, 2, 10),
            Trade(10, TradeSide.Buy, 2, 200, 4, 5),
            Trade(20, TradeSide.Sell, 4, 200)
        ], [], frequency: 0).Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalPnl, Is.EqualTo(194d));
            Assert.That(result.TotalPnlPercent, Is.EqualTo(32.33d));
            Assert.That(result.AnnualizedPnlPercent, Is.EqualTo(825.29d));
        });
    }

    [Test]
    public void Calculate_ChangesDailyCouponAtPeriodBoundary()
    {
        var result = Calculate([Trade(20, TradeSide.Buy, 1), Trade(40, TradeSide.Sell, 1)],
            [Coupon(0, 30, 30), Coupon(30, 60, 60)], bondType: "Флоатер").Single();
        Assert.That(result.CouponIncome, Is.EqualTo(30d));
        Assert.That(result.CalculatorKind, Is.EqualTo("Floater"));
    }

    [Test]
    public void Floater_UsesPreviousCouponAmountForMissingCurrentPeriod()
    {
        var previousPeriod = new ClosedSessionCoupon(
            new DateOnly(2025, 12, 15), new DateOnly(2026, 6, 15), 89.22d, null, "RUB");
        var currentPeriod = new ClosedSessionCoupon(
            new DateOnly(2026, 6, 15), new DateOnly(2026, 12, 14), null, null, "RUB");

        var result = CalculateForDates(new DateOnly(2026, 8, 19), new DateOnly(2026, 8, 24),
            [previousPeriod, currentPeriod]);

        Assert.Multiple(() =>
        {
            Assert.That(result.CouponIncome, Is.EqualTo((89.22d * 5d / 182d).Round()).Within(1e-10d));
            Assert.That(result.TotalPnl, Is.EqualTo((89.22d * 5d / 182d).Round()).Within(1e-10d));
            Assert.That(result.TotalPnlPercent, Is.EqualTo((89.22d * 5d / 182d).Round()).Within(1e-10d));
            Assert.That(result.AnnualizedPnlPercent,
                Is.EqualTo((89.22d * 5d / 182d * 365d / 5d).Round()).Within(1e-10d));
            Assert.That(result.HoldingDays, Is.EqualTo(5));
            Assert.That(result.CalculatorKind, Is.EqualTo("Floater"));
            Assert.That(result.IsComplete, Is.True);
        });
    }

    [Test]
    public void Floater_UsesLastKnownAmountAcrossConsecutiveMissingPeriods()
    {
        var result = CalculateForDates(new DateOnly(2026, 1, 12), new DateOnly(2026, 1, 25), [
            new(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 11), 20, null, "RUB"),
            new(new DateOnly(2026, 1, 11), new DateOnly(2026, 1, 21), null, null, "RUB"),
            new(new DateOnly(2026, 1, 21), new DateOnly(2026, 1, 31), null, null, "RUB")
        ]);

        Assert.That(result.CouponIncome, Is.EqualTo(20d * 9d / 10d + 20d * 4d / 10d));
    }

    [Test]
    public void Floater_KnownCurrentPeriodAmountOverridesFallback()
    {
        var result = CalculateForDates(new DateOnly(2026, 1, 18), new DateOnly(2026, 1, 23), [
            new(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10), 30, null, "RUB"),
            new(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 20), null, null, "RUB"),
            new(new DateOnly(2026, 1, 20), new DateOnly(2026, 1, 30), 50, null, "RUB")
        ]);

        Assert.That(result.CouponIncome, Is.EqualTo(30d * 2d / 10d + 50d * 3d / 10d));
    }

    [Test]
    public void Floater_UsesApplicableRubleValueBeforePreviousCouponFallback()
    {
        var result = CalculateForDates(new DateOnly(2026, 8, 19), new DateOnly(2026, 8, 24), [
            new(new DateOnly(2025, 12, 15), new DateOnly(2026, 6, 15), 89.22d, null, "RUB"),
            new(new DateOnly(2026, 6, 15), new DateOnly(2026, 12, 14), null, 1_000d, "RUB")
        ]);

        Assert.That(result.CouponIncome, Is.EqualTo((1_000d * 5d / 182d).Round()).Within(1e-10d));
    }

    [Test]
    public void Floater_WithoutEarlierKnownCouponRemainsIncomplete()
    {
        var result = CalculateForDates(new DateOnly(2026, 8, 19), new DateOnly(2026, 8, 24), [
            new(new DateOnly(2026, 6, 15), new DateOnly(2026, 12, 14), null, null, "RUB")
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.CouponIncome, Is.Null);
            Assert.That(result.TotalPnl, Is.Null);
            Assert.That(result.IsComplete, Is.False);
        });
    }

    [Test]
    public void DefaultBond_DoesNotUseFloaterCouponFallback()
    {
        var result = CalculateForDates(new DateOnly(2026, 8, 19), new DateOnly(2026, 8, 24), [
            new(new DateOnly(2025, 12, 15), new DateOnly(2026, 6, 15), 89.22d, null, "RUB"),
            new(new DateOnly(2026, 6, 15), new DateOnly(2026, 12, 14), null, null, "RUB")
        ], bondType: "");

        Assert.Multiple(() =>
        {
            Assert.That(result.CalculatorKind, Is.EqualTo("Default"));
            Assert.That(result.CouponIncome, Is.Null);
            Assert.That(result.IsComplete, Is.False);
        });
    }

    [Test]
    public void Floater_IdenticalPreviousCouponDuplicatesDoNotChangeFallbackIncome()
    {
        var known = new ClosedSessionCoupon(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 11), 20, null, "RUB");
        var missing = new ClosedSessionCoupon(
            new DateOnly(2026, 1, 11), new DateOnly(2026, 1, 21), null, null, "RUB");
        var result = CalculateForDates(new DateOnly(2026, 1, 11), new DateOnly(2026, 1, 16), [
            known, known, missing, missing
        ]);

        Assert.That(result.CouponIncome, Is.EqualTo(10d));
    }

    [Test]
    public void Floater_OverlappingPeriodsRemainIncomplete()
    {
        var result = CalculateForDates(new DateOnly(2026, 8, 19), new DateOnly(2026, 8, 24), [
            new(new DateOnly(2025, 12, 15), new DateOnly(2026, 12, 14), 89.22d, null, "RUB"),
            new(new DateOnly(2026, 6, 15), new DateOnly(2026, 12, 14), null, null, "RUB")
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.CouponIncome, Is.Null);
            Assert.That(result.IsComplete, Is.False);
        });
    }

    [Test]
    public void Floater_KnownCouponWithMissingExchangeRateRemainsIncomplete()
    {
        var result = CalculateForDates(new DateOnly(2026, 8, 19), new DateOnly(2026, 8, 24), [
            new(new DateOnly(2025, 12, 15), new DateOnly(2026, 6, 15), 89.22d, null, "RUB"),
            new(new DateOnly(2026, 6, 15), new DateOnly(2026, 12, 14), 100d, null, "USD")
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.CalculatorKind, Is.EqualTo("Floater"));
            Assert.That(result.CouponIncome, Is.Null);
            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.IncompleteReason, Does.Contain("курс"));
        });
    }

    [Test]
    public void Floater_FallbackCouponUsesHistoricalDailyExchangeRates()
    {
        var result = CalculateForDates(new DateOnly(2026, 8, 19), new DateOnly(2026, 8, 24), [
            new(new DateOnly(2025, 12, 15), new DateOnly(2026, 6, 15), 30d, null, "USD"),
            new(new DateOnly(2026, 6, 15), new DateOnly(2026, 12, 14), null, null, "USD")
        ], currencyId: "EUR", currencyRates: [
            new(new DateOnly(2026, 8, 19), "USD", 90),
            new(new DateOnly(2026, 8, 22), "USD", 100),
            new(new DateOnly(2026, 8, 19), "EUR", 1)
        ]);

        Assert.That(result.CouponIncome, Is.EqualTo(((3d * 30d * 90d + 2d * 30d * 100d) / 182d).Round()));
    }

    [Test]
    public void Calculate_SameDayReopeningCreatesAnotherSessionAndIgnoresOpenTail()
    {
        var result = Calculate([
            Trade(0, TradeSide.Buy, 10), Trade(10, TradeSide.Buy, 5),
            Trade(10, TradeSide.Sell, 10), Trade(20, TradeSide.Sell, 5),
            Trade(21, TradeSide.Buy, 3)
        ], [Coupon(0, 30, 30)]);
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(x => x.CouponIncome), Is.EqualTo(new double?[] { 100, 50 }));
        Assert.That(result[1].StartDate, Is.EqualTo(Start.AddDays(10)));
    }

    [Test]
    public void Calculate_UnknownOrMissingCouponKeepsBodyButHidesTotal()
    {
        var result = Calculate([Trade(0, TradeSide.Buy, 1), Trade(20, TradeSide.Sell, 1, 110)],
            [Coupon(0, 10, 10), Coupon(10, 30, null)]).Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.BodyPnl, Is.EqualTo(10d));
            Assert.That(result.CouponIncome, Is.Null);
            Assert.That(result.TotalPnl, Is.Null);
            Assert.That(result.IncompleteReason, Is.Not.Empty);
        });
    }

    [Test]
    public void Calculate_IncludesPartialAmortizationButExcludesFinalRedemption()
    {
        var result = Calculate([Trade(0, TradeSide.Buy, 1, 1000), Trade(30, TradeSide.Sell, 1, 820)],
            [Coupon(0, 40, 40)], [Amort(10, 200), Amort(20, 800)], initialFaceValue: 1000).Single();
        Assert.That(result.AmortizationIncome, Is.EqualTo(200d));
        Assert.That(result.BodyPnl, Is.EqualTo(20d));
    }

    [Test]
    public void Calculate_UsesQuantityAfterSalesForAmortizationAndExcludesSameDayPurchases()
    {
        var result = Calculate([
            Trade(0, TradeSide.Buy, 10, 1000), Trade(10, TradeSide.Sell, 4, 1000),
            Trade(10, TradeSide.Buy, 2, 800), Trade(20, TradeSide.Sell, 8, 800)
        ], [Coupon(0, 30, 30)], [Amort(10, 200)], initialFaceValue: 1000).Single();
        Assert.That(result.AmortizationIncome, Is.EqualTo(1200d));
    }

    [Test]
    public void Calculate_UsesPercentWhenAmortizationValueIsMissing()
    {
        var result = Calculate([
            Trade(0, TradeSide.Buy, 2, 1000),
            Trade(20, TradeSide.Sell, 2, 1000)
        ], [Coupon(0, 30, 30)],
            [new ClosedSessionAmortization(Start.AddDays(10), null, null, 10, 1000, "RUB")],
            initialFaceValue: 1000).Single();

        Assert.That(result.AmortizationIncome, Is.EqualTo(200d));
        Assert.That(result.IsComplete, Is.True);
    }

    [Test]
    public void Calculate_ExcludesFinalRepaymentAfterPreSessionAmortizations()
    {
        var result = Calculate([
            Trade(0, TradeSide.Buy, 1, 100),
            Trade(10, TradeSide.Sell, 1, 100)
        ], [Coupon(-1, 11, 120)], [
            new ClosedSessionAmortization(Start.AddDays(-10), 900, null, null, 1000, "RUB"),
            new ClosedSessionAmortization(Start.AddDays(5), 100, null, null, 1000, "RUB")
        ], initialFaceValue: 1000).Single();

        Assert.That(result.AmortizationIncome, Is.EqualTo(0d));
    }

    [Test]
    public void Calculate_UsesPostSaleQuantityForAmortizationOnPartialSaleDate()
    {
        var result = Calculate([
            Trade(0, TradeSide.Buy, 10, 1000),
            Trade(10, TradeSide.Sell, 4, 1000),
            Trade(20, TradeSide.Sell, 6, 1000)
        ], [Coupon(0, 30, 30)], [Amort(10, 100)], initialFaceValue: 1000).Single();

        Assert.That(result.AmortizationIncome, Is.EqualTo(600d));
    }

    [Test]
    public void Calculate_UnknownAmortizationExchangeRateHidesBodyAndTotal()
    {
        var input = new ClosedSessionInstrument("TEST", "TQCB", "RUB", "Test bond", "USD", "", "", 12,
            1000, null,
            [Trade(0, TradeSide.Buy, 1), Trade(20, TradeSide.Sell, 1)],
            [Coupon(0, 30, 30)],
            [new ClosedSessionAmortization(Start.AddDays(10), 10, null, null, 1000, "USD")], []);

        var result = new ClosedPositionSessionCalculator().Calculate(input, DateTimeOffset.UtcNow).Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.AmortizationIncome, Is.Null);
            Assert.That(result.BodyPnl, Is.Null);
            Assert.That(result.TotalPnl, Is.Null);
            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.IncompleteReason, Does.Contain("курс"));
        });
    }

    [Test]
    public void Calculate_SameDayRoundTripHasNoCouponOrAnnualizedIncome()
    {
        var result = Calculate([Trade(0, TradeSide.Sell, 1, 110), Trade(0, TradeSide.Buy, 1)], []).Single();
        Assert.That(result.CouponIncome, Is.EqualTo(0d));
        Assert.That(result.TotalPnl, Is.EqualTo(10d));
        Assert.That(result.AnnualizedPnlPercent, Is.Null);
    }

    [Test]
    public void Calculate_RejectsSaleBeyondAvailablePurchases()
    {
        Assert.That(() => Calculate([Trade(0, TradeSide.Buy, 1), Trade(10, TradeSide.Sell, 2)], []),
            Throws.InvalidOperationException);
    }

    [Test]
    public void Calculate_ZeroCouponBondDoesNotNeedCouponHistory()
    {
        var result = Calculate([Trade(0, TradeSide.Buy, 1), Trade(10, TradeSide.Sell, 1, 110)], [], frequency: 0).Single();
        Assert.That(result.IsComplete, Is.True);
        Assert.That(result.TotalPnl, Is.EqualTo(10d));
    }

    [Test]
    public void Calculate_OverlappingPeriodsDoNotDoubleCountIncome()
    {
        var result = Calculate([Trade(0, TradeSide.Buy, 1), Trade(20, TradeSide.Sell, 1)],
            [Coupon(0, 30, 30), Coupon(10, 40, 60)]).Single();
        Assert.That(result.CouponIncome, Is.Null);
        Assert.That(result.IsComplete, Is.False);
    }

    [Test]
    public void Calculate_UsesAllCleanPurchasesAsPercentDenominator()
    {
        var result = Calculate([
            Trade(0, TradeSide.Buy, 10, 1000), Trade(10, TradeSide.Sell, 5, 1100),
            Trade(20, TradeSide.Buy, 5, 1000), Trade(30, TradeSide.Sell, 10, 1100)
        ], [], frequency: 0).Single();
        Assert.That(result.BoughtCleanAmount, Is.EqualTo(15000d));
        Assert.That(result.TotalPnl, Is.EqualTo(1500d));
        Assert.That(result.TotalPnlPercent, Is.EqualTo(10d));
        Assert.That(result.AnnualizedPnlPercent, Is.EqualTo(219d));
    }

    [Test]
    public void Calculate_LossIncludesBothCommissionsAndPreservesFractionalQuantity()
    {
        var result = Calculate([
            Trade(0, TradeSide.Buy, 2.5d, 100, 1.25d),
            Trade(10, TradeSide.Sell, 2.5d, 90, 2.25d)
        ], [], frequency: 0).Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.BoughtQuantity, Is.EqualTo(2.5d));
            Assert.That(result.BoughtCleanAmount, Is.EqualTo(250d));
            Assert.That(result.SoldCleanAmount, Is.EqualTo(225d));
            Assert.That(result.Commission, Is.EqualTo(3.5d));
            Assert.That(result.BodyPnl, Is.EqualTo(-25d));
            Assert.That(result.TotalPnl, Is.EqualTo(-28.5d));
            Assert.That(result.TotalPnlPercent, Is.EqualTo(-11.4d));
            Assert.That(result.AnnualizedPnlPercent, Is.EqualTo(-414.03d));
            Assert.That(result.IsComplete, Is.True);
        });
    }

    [Test]
    public void Calculate_SplitsOversizedSameDaySaleAndCommissionAcrossClosedAndReopenedSessions()
    {
        var result = Calculate([
            Trade(0, TradeSide.Buy, 2, 100, 2),
            Trade(10, TradeSide.Sell, 3, 110, 3),
            Trade(10, TradeSide.Buy, 2, 100, 2),
            Trade(20, TradeSide.Sell, 1, 120)
        ], [], frequency: 0);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].BoughtQuantity, Is.EqualTo(2d));
            Assert.That(result[0].BoughtCleanAmount, Is.EqualTo(200d));
            Assert.That(result[0].SoldCleanAmount, Is.EqualTo(220d));
            Assert.That(result[0].Commission, Is.EqualTo(4d));
            Assert.That(result[0].TotalPnl, Is.EqualTo(16d));
            Assert.That(result[0].TotalPnlPercent, Is.EqualTo(8d));

            Assert.That(result[1].StartDate, Is.EqualTo(Start.AddDays(10)));
            Assert.That(result[1].BoughtQuantity, Is.EqualTo(2d));
            Assert.That(result[1].BoughtCleanAmount, Is.EqualTo(200d));
            Assert.That(result[1].SoldCleanAmount, Is.EqualTo(230d));
            Assert.That(result[1].Commission, Is.EqualTo(3d));
            Assert.That(result[1].TotalPnl, Is.EqualTo(27d));
            Assert.That(result[1].TotalPnlPercent, Is.EqualTo(13.5d));
        });
    }

    [Test]
    public void Calculate_ZeroPurchaseBasisLeavesReturnPercentagesUndefined()
    {
        var result = Calculate([
            Trade(0, TradeSide.Buy, 1, 0),
            Trade(1, TradeSide.Sell, 1, 0)
        ], [], frequency: 0).Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.BoughtCleanAmount, Is.Zero);
            Assert.That(result.BodyPnl, Is.Zero);
            Assert.That(result.TotalPnl, Is.Zero);
            Assert.That(result.TotalPnlPercent, Is.Null);
            Assert.That(result.AnnualizedPnlPercent, Is.Null);
        });
    }

    [Test]
    public void Calculate_UsesNewCouponOnBoundaryDateAndExcludesSaleDate()
    {
        var result = Calculate([
            Trade(5, TradeSide.Buy, 1),
            Trade(15, TradeSide.Sell, 1)
        ], [Coupon(0, 10, 20), Coupon(10, 20, 40)]).Single();

        Assert.That(result.CouponIncome, Is.EqualTo(30d));
    }

    [Test]
    public void Calculate_DuplicateIdenticalPeriodsAreCountedOnce()
    {
        var period = Coupon(0, 30, 30);
        var result = Calculate([Trade(0, TradeSide.Buy, 1), Trade(20, TradeSide.Sell, 1)],
            [period, period]).Single();
        Assert.That(result.CouponIncome, Is.EqualTo(20d));
    }

    [Test]
    public void Calculate_MissingPeriodDoesNotReuseLatestCoupon()
    {
        var result = Calculate([Trade(0, TradeSide.Buy, 1), Trade(20, TradeSide.Sell, 1)],
            [Coupon(0, 10, 10)]).Single();
        Assert.That(result.CouponIncome, Is.Null);
        Assert.That(result.TotalPnl, Is.Null);
    }

    [Test]
    public void ZoCalculator_PrefersPublishedRubleCouponAmount()
    {
        var input = Instrument("USD", "RUB", [new ClosedSessionCoupon(Start, Start.AddDays(30), 3, 300, "USD")], []);
        var result = new ClosedPositionSessionCalculator().Calculate(input, DateTimeOffset.UtcNow).Single();
        Assert.That(result.CalculatorKind, Is.EqualTo("Zo"));
        Assert.That(result.CouponIncome, Is.EqualTo(200d));
    }

    [Test]
    public void ZoCalculator_UsesHistoricalDailyRatesAndNeverFutureRate()
    {
        var input = Instrument("USD", "RUB", [new ClosedSessionCoupon(Start, Start.AddDays(30), 30, null, "USD")],
            [new(Start, "USD", 90), new(Start.AddDays(10), "USD", 100), new(Start.AddDays(21), "USD", 1000)]);
        var result = new ClosedPositionSessionCalculator().Calculate(input, DateTimeOffset.UtcNow).Single();
        Assert.That(result.CouponIncome, Is.EqualTo(1900d));
    }

    [Test]
    public void ZoCalculator_MissingConversionRateKeepsBodyAndHidesTotal()
    {
        var input = Instrument("USD", "RUB", [new ClosedSessionCoupon(Start, Start.AddDays(30), 30, null, "USD")], []);
        var result = new ClosedPositionSessionCalculator().Calculate(input, DateTimeOffset.UtcNow).Single();
        Assert.That(result.BodyPnl, Is.EqualTo(0d));
        Assert.That(result.CouponIncome, Is.Null);
        Assert.That(result.TotalPnl, Is.Null);
    }

    private static ClosedSessionInstrument Instrument(string faceUnit, string currency,
        IReadOnlyList<ClosedSessionCoupon> coupons, IReadOnlyList<ClosedSessionCurrencyRate> rates)
        => new("TEST", "TQCB", currency, "Test", faceUnit, "", "", 12, 1000, null,
            [Trade(0, TradeSide.Buy, 1), Trade(20, TradeSide.Sell, 1)], coupons, [], rates);

    private static IReadOnlyList<ClosedPositionSession> Calculate(
        IReadOnlyList<ClosedSessionTrade> trades,
        IReadOnlyList<ClosedSessionCoupon> coupons,
        IReadOnlyList<ClosedSessionAmortization>? amortizations = null,
        string bondType = "", double initialFaceValue = 1000, int? frequency = 12)
        => new ClosedPositionSessionCalculator().Calculate(new ClosedSessionInstrument(
            "TEST", "TQCB", "RUB", "Test bond", "RUB", bondType, "", frequency,
            initialFaceValue, null, trades, coupons, amortizations ?? [], []), DateTimeOffset.UtcNow);

    private static ClosedPositionSession CalculateForDates(DateOnly buyDate, DateOnly sellDate,
        IReadOnlyList<ClosedSessionCoupon> coupons, string bondType = "Флоатер", string faceUnit = "RUB",
        IReadOnlyList<ClosedSessionCurrencyRate>? currencyRates = null, string currencyId = "RUB")
    {
        var instrument = new ClosedSessionInstrument("TEST", "TQCB", currencyId, "Test bond", faceUnit,
            bondType, "", 12, 1000, null,
            [new(Guid.NewGuid(), buyDate, TradeSide.Buy, 1, 100, 0),
             new(Guid.NewGuid(), sellDate, TradeSide.Sell, 1, 100, 0)],
            coupons, [], currencyRates ?? []);
        return new ClosedPositionSessionCalculator().Calculate(instrument, DateTimeOffset.UtcNow).Single();
    }

    private static ClosedSessionTrade Trade(int day, TradeSide side, double quantity, double price = 100,
        double commission = 0, double accruedInterest = 0)
        => new(Guid.NewGuid(), Start.AddDays(day), side, quantity, price, commission, accruedInterest);

    private static ClosedSessionCoupon Coupon(int from, int to, double? value) =>
        new(Start.AddDays(from), Start.AddDays(to), value, null, "RUB");

    private static ClosedSessionAmortization Amort(int day, double value) =>
        new(Start.AddDays(day), value, null, null, 1000, "RUB");

    private static ClosedSessionInstrument ScenarioInstrument(string secId,
        IReadOnlyList<ClosedSessionTrade> trades, IReadOnlyList<ClosedSessionCoupon> coupons,
        IReadOnlyList<ClosedSessionAmortization>? amortizations = null, double initialFaceValue = 1000)
        => new(secId, "TQCB", "RUB", $"{secId} bond", "RUB", "", "", 12, initialFaceValue, null,
            trades, coupons, amortizations ?? [], []);

    private static ClosedSessionTrade FixedTrade(Guid id, DateOnly date, TradeSide side, double quantity,
        double price, double commission = 0)
        => new(id, date, side, quantity, price, commission);

    private static ClosedSessionCoupon FixedCoupon(DateOnly from, DateOnly to, double? value)
        => new(from, to, value, null, "RUB");

    private static ClosedSessionAmortization FixedAmortization(DateOnly date, double value)
        => new(date, value, null, null, 1000, "RUB");
}
