using NUnit.Framework;
using Popo.Core.Calculators;
using Popo.Core.Recommendations;

namespace Popo.Tests;

[TestFixture]
public sealed class YieldCalculatorTests
{
    [Test]
    public void TryCalculate_ReturnsYieldToMaturityFromDirtyPriceAndFutureCashFlows()
    {
        var ytm = YieldCalculator.TryCalculate(
            new DateOnly(2026, 1, 1),
            1_000d,
            [new CashFlow(1_100d, new DateOnly(2027, 1, 1))]);

        Assert.That(ytm, Is.EqualTo(10d).Within(1e-10));
    }

    [Test]
    public void TryCalculate_IgnoresCashFlowsOnOrBeforeValuationDate()
    {
        var valuationDate = new DateOnly(2026, 8, 19);

        var ytm = YieldCalculator.TryCalculate(
            valuationDate,
            1_000d,
            [
                new CashFlow(100d, valuationDate.AddDays(-1)),
                new CashFlow(100d, valuationDate),
                new CashFlow(1_100d, valuationDate.AddYears(1))
            ]);

        Assert.That(ytm, Is.EqualTo(10d).Within(1e-10));
    }

    [Test]
    public void TryCalculate_ReturnsExpectedYieldForRu000A109U97AfterCouponCutoff()
    {
        var ytm = YieldCalculator.TryCalculate(
            new DateOnly(2026, 8, 19),
            1_002.82,
            [
                new CashFlow(12.53, new DateOnly(2026, 9, 18)),
                new CashFlow(12.53, new DateOnly(2026, 10, 18)),
                new CashFlow(1_000, new DateOnly(2026, 10, 18))
            ]);

        Assert.That(ytm, Is.EqualTo(14.37d).Within(0.005d));
    }

    [Test]
    public void TryCalculate_ReturnsExpectedYieldForRu000A108GR8FromSettlementDate()
    {
        var settlementDate = new DateOnly(2026, 8, 20);

        var ytm = YieldCalculator.TryCalculate(
            settlementDate,
            1_001.90,
            [
                new CashFlow(38.66, settlementDate),
                new CashFlow(38.66, new DateOnly(2026, 11, 19)),
                new CashFlow(38.66, new DateOnly(2027, 2, 18)),
                new CashFlow(38.66, new DateOnly(2027, 5, 20)),
                new CashFlow(1_000, new DateOnly(2027, 5, 20))
            ]);

        Assert.That(ytm, Is.EqualTo(16.13d).Within(0.005d));
    }

    [Test]
    public void TryCalculate_DoesNotRoundIrregularDateYield()
    {
        var settlementDate = new DateOnly(2026, 1, 1);
        var redemptionDate = settlementDate.AddDays(182);

        var yield = YieldCalculator.TryCalculate(
            settlementDate,
            1_000d,
            [new CashFlow(1_100d, redemptionDate)]);

        Assert.That(yield, Is.EqualTo(21.0633821537084d).Within(1e-10));
        Assert.That(ReversePresentValue(settlementDate, yield!.Value, [new CashFlow(1_100d, redemptionDate)]),
            Is.EqualTo(1_000d).Within(1e-8));
    }

    [Test]
    public void TryCalculate_SupportsYieldAboveOldFiveHundredPercentLimit()
    {
        var settlementDate = new DateOnly(2026, 1, 1);

        var yield = YieldCalculator.TryCalculate(
            settlementDate,
            100d,
            [new CashFlow(1_100d, settlementDate.AddYears(1))]);

        Assert.That(yield, Is.EqualTo(1_000d).Within(1e-8));
    }

    [Test]
    public void TryCalculate_SupportsNegativeYieldAboveMinusOneDomainBoundary()
    {
        var settlementDate = new DateOnly(2026, 1, 1);

        var yield = YieldCalculator.TryCalculate(
            settlementDate,
            2_000d,
            [new CashFlow(1_000d, settlementDate.AddYears(1))]);

        Assert.That(yield, Is.EqualTo(-50d).Within(1e-10));
    }

    [Test]
    public void TryCalculate_DoesNotCollapseSmallNonZeroYieldToZero()
    {
        var settlementDate = new DateOnly(2026, 1, 1);

        var yield = YieldCalculator.TryCalculate(
            settlementDate,
            1_000d / (1d + 1e-10),
            [new CashFlow(1_000d, settlementDate.AddYears(1))]);

        Assert.That(yield, Is.EqualTo(1e-8d).Within(1e-11));
    }

    [TestCase(0d)]
    [TestCase(-1d)]
    public void TryCalculate_ReturnsNullForNonPositiveDirtyPrice(double dirtyPrice)
    {
        var settlementDate = new DateOnly(2026, 1, 1);

        var yield = YieldCalculator.TryCalculate(
            settlementDate,
            dirtyPrice,
            [new CashFlow(1_100d, settlementDate.AddYears(1))]);

        Assert.That(yield, Is.Null);
    }

    [Test]
    public void TryCalculate_ReturnsNullWithoutPositiveFutureCashFlow()
    {
        var settlementDate = new DateOnly(2026, 1, 1);

        var yield = YieldCalculator.TryCalculate(
            settlementDate,
            1_000d,
            [
                new CashFlow(1_100d, settlementDate),
                new CashFlow(0d, settlementDate.AddYears(1))
            ]);

        Assert.That(yield, Is.Null);
    }

    private static double ReversePresentValue(
        DateOnly settlementDate,
        double yieldPercent,
        IEnumerable<CashFlow> flows)
    {
        var annualRate = yieldPercent / 100d;

        return flows
            .Where(x => x.Date > settlementDate)
            .Sum(x => x.ValueInRub / Math.Pow(
                1d + annualRate,
                (x.Date.DayNumber - settlementDate.DayNumber) / 365d));
    }
}
