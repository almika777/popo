using NUnit.Framework;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;

namespace Popo.Tests;

[TestFixture]
public sealed class FloaterPositionCalculatorTests
{
    private static readonly DateOnly AsOf = new(2027, 1, 1);

    [Test]
    [TestCase(50d, 100d)]
    [TestCase(100d, 50d)]
    public void Calculate_UsesEachPublishedCouponAmountInsteadOfTheLatestCouponForEveryPeriod(
        double firstCoupon,
        double secondCoupon)
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 10, 1_000)],
            1_000,
            0,
            [
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 1), firstCoupon,
                    new DateOnly(2026, 6, 15)),
                Coupon(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), secondCoupon,
                    new DateOnly(2026, 9, 15)),
                Coupon(new DateOnly(2027, 3, 31), new DateOnly(2026, 10, 1), null, new DateOnly(2027, 3, 15))
            ]);

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.EqualTo((firstCoupon + secondCoupon) * 10));
            Assert.That(result.Income.CouponIncome, Is.EqualTo((firstCoupon + secondCoupon) * 10));
            Assert.That(result.Income.TotalPnl, Is.EqualTo((firstCoupon + secondCoupon) * 10));
            Assert.That(result.Income.TotalPnlPercent, Is.EqualTo((firstCoupon + secondCoupon) / 10));
            Assert.That(result.Income.AnnualizedTotalPnlPercent, Is.EqualTo((firstCoupon + secondCoupon) / 10));
            Assert.That(result.Income.IsCouponIncomeComplete, Is.True);
        });
    }

    [Test]
    public void Calculate_NetsHistoricalPaymentsAndBothAccruedInterestAmountsAgainstCleanPnlAndCommission()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 10, 1_000, accruedInterest: 5, commission: 10)],
            970,
            7,
            [
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 1), 50, new DateOnly(2026, 6, 15)),
                Coupon(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), 100, new DateOnly(2026, 9, 15)),
                Coupon(new DateOnly(2027, 3, 31), new DateOnly(2026, 10, 1), null, new DateOnly(2027, 3, 15))
            ]);

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.EqualTo(1_500));
            Assert.That(result.Income.PaidBuyAccruedInterest, Is.EqualTo(50));
            Assert.That(result.Income.CurrentAccruedInterest, Is.EqualTo(70));
            Assert.That(result.Income.CouponIncome, Is.EqualTo(1_520));
            Assert.That(result.Income.UnrealizedPnl, Is.EqualTo(-300));
            Assert.That(result.Income.TotalPnl, Is.EqualTo(1_210));
            Assert.That(result.Income.TotalPnlPercent, Is.EqualTo(12.1).Within(0.000001));
            Assert.That(result.Income.AnnualizedTotalPnlPercent, Is.EqualTo(12.1).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_DoesNotUseCurrentCouponValueToRewritePublishedPeriods()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 10, 1_000)],
            1_000,
            0,
            [
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 1), 50,
                    new DateOnly(2026, 6, 15)),
                Coupon(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), 100,
                    new DateOnly(2026, 9, 15)),
                Coupon(new DateOnly(2027, 3, 31), new DateOnly(2026, 10, 1), null,
                    new DateOnly(2027, 3, 15))
            ]) with
        {
            CouponValue = 500
        };

        var baseline = new FloaterPositionCalculator().Calculate(input with { CouponValue = 0 });
        var changedCurrentCoupon = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(baseline.Income!.HistoricalCouponIncome, Is.EqualTo(1_500));
            Assert.That(changedCurrentCoupon.Income!.HistoricalCouponIncome, Is.EqualTo(1_500));
            Assert.That(changedCurrentCoupon.Income.CouponIncome, Is.EqualTo(baseline.Income.CouponIncome));
        });
    }

    [Test]
    public void Calculate_IncludesPaymentOnValuationDateOnceAndExcludesKnownFuturePayment()
    {
        var valuationDate = new DateOnly(2026, 6, 30);
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000)],
            1_000,
            0,
            [
                Coupon(new DateOnly(2026, 3, 31), new DateOnly(2026, 1, 1), 50,
                    new DateOnly(2026, 3, 15)),
                Coupon(valuationDate, new DateOnly(2026, 4, 1), 100,
                    new DateOnly(2026, 6, 15)),
                Coupon(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), 1_000,
                    new DateOnly(2026, 9, 15))
            ],
            asOf: valuationDate,
            nextCouponDate: new DateOnly(2026, 9, 30));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.EqualTo(150));
            Assert.That(result.Income.IsCouponIncomeComplete, Is.True);
        });
    }

    [Test]
    public void Calculate_UsesProportionallyReducedPurchaseCohorts()
    {
        var input = Input(
            [
                Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 10, 1_000, accruedInterest: 5, commission: 20),
                Trade(new DateOnly(2026, 2, 1), TradeSide.Buy, 5, 1_100, accruedInterest: 8, commission: 15),
                Trade(new DateOnly(2026, 7, 1), TradeSide.Sell, 12, 1_050)
            ],
            1_050,
            10,
            [
                Coupon(new DateOnly(2026, 3, 31), new DateOnly(2026, 1, 1), 25, new DateOnly(2026, 3, 15)),
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 4, 1), 40, new DateOnly(2026, 6, 15)),
                Coupon(new DateOnly(2027, 3, 31), new DateOnly(2026, 7, 1), null, new DateOnly(2027, 3, 15))
            ]);

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.RemainingQuantity, Is.EqualTo(3));
            Assert.That(result.AverageBuyPriceAtCurrentFaceValue, Is.EqualTo(1_033.333333).Within(0.000001));
            Assert.That(result.Income.RemainingCleanCost, Is.EqualTo(3_100));
            Assert.That(result.Income.PaidBuyCommission, Is.EqualTo(7));
            Assert.That(result.Income.PaidBuyAccruedInterest, Is.EqualTo(18).Within(0.000001));
            Assert.That(result.Income.HistoricalCouponIncome, Is.EqualTo(195));
            Assert.That(result.Income.CurrentAccruedInterest, Is.EqualTo(30));
            Assert.That(result.Income.CouponIncome, Is.EqualTo(207).Within(0.000001));
            Assert.That(result.Income.UnrealizedPnl, Is.EqualTo(50));
            Assert.That(result.Income.TotalPnl, Is.EqualTo(250).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_ReturnsPriceResultButHidesCouponBasedMetricsWhenAPastPaymentAmountIsUnknown()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000)],
            1_050,
            0,
            [
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 1), null,
                    new DateOnly(2026, 6, 15)),
                Coupon(new DateOnly(2027, 3, 31), new DateOnly(2026, 7, 1), null,
                    new DateOnly(2027, 3, 15))
            ]);

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Valuation.UnrealizedPnl, Is.EqualTo(50));
            Assert.That(result.Income!.IsCouponIncomeComplete, Is.False);
        });
    }

    [Test]
    public void Calculate_DoesNotAttributeACouponToALotBoughtAfterItsRecordDate()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 4, 15), TradeSide.Buy, 1, 1_000)],
            1_000,
            0,
            [
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 4, 1), 100,
                    new DateOnly(2026, 4, 14)),
                Coupon(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), null,
                    new DateOnly(2026, 9, 15))
            ],
            asOf: new DateOnly(2026, 6, 30),
            nextCouponDate: new DateOnly(2026, 9, 30));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.Zero);
            Assert.That(result.Income.IsCouponIncomeComplete, Is.True);
        });
    }

    [Test]
    public void Calculate_AttributesPaymentToLotBoughtInsideThePeriodBeforeItsRecordDate()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 4, 15), TradeSide.Buy, 1, 1_000)],
            1_000,
            0,
            [
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 4, 1), 100,
                    new DateOnly(2026, 4, 25)),
                Coupon(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), null,
                    new DateOnly(2026, 9, 15))
            ],
            asOf: new DateOnly(2026, 6, 30),
            nextCouponDate: new DateOnly(2026, 9, 30));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.EqualTo(100));
            Assert.That(result.Income.IsCouponIncomeComplete, Is.True);
        });
    }

    [Test]
    public void Calculate_HidesCouponMetricsWhenRecordDateIsMissingForAnInsidePeriodPurchase()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 4, 15), TradeSide.Buy, 1, 1_000)],
            1_000,
            0,
            [
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 4, 1), 100, null),
                Coupon(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), null,
                    new DateOnly(2026, 9, 15))
            ],
            asOf: new DateOnly(2026, 6, 30),
            nextCouponDate: new DateOnly(2026, 9, 30));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Valuation.UnrealizedPnl, Is.Zero);
            Assert.That(result.Income!.HistoricalCouponIncome, Is.Zero);
            Assert.That(result.Income.IsCouponIncomeComplete, Is.False);
        });
    }

    [Test]
    public void Calculate_DoesNotGiveReopenedPositionCouponsFromTheEarlierHoldingPeriod()
    {
        var input = Input(
            [
                Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000),
                Trade(new DateOnly(2026, 4, 30), TradeSide.Sell, 1, 1_000),
                Trade(new DateOnly(2026, 5, 1), TradeSide.Buy, 1, 1_000)
            ],
            1_000,
            0,
            [
                Coupon(new DateOnly(2026, 3, 31), new DateOnly(2026, 1, 1), 100,
                    new DateOnly(2026, 3, 15)),
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 4, 1), 200,
                    new DateOnly(2026, 6, 15)),
                Coupon(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), null,
                    new DateOnly(2026, 9, 15))
            ],
            asOf: new DateOnly(2026, 7, 2),
            nextCouponDate: new DateOnly(2026, 9, 30));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.That(result.Income!.HistoricalCouponIncome, Is.EqualTo(200));
    }

    [Test]
    public void Calculate_TreatsPublishedZeroAsKnownCouponPayment()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000)],
            1_000,
            0,
            [
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 1), 0,
                    new DateOnly(2026, 6, 15)),
                Coupon(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), null,
                    new DateOnly(2026, 9, 15))
            ],
            asOf: new DateOnly(2026, 6, 30),
            nextCouponDate: new DateOnly(2026, 9, 30));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.Zero);
            Assert.That(result.Income.IsCouponIncomeComplete, Is.True);
        });
    }

    [Test]
    public void Calculate_UsesPublishedRubleCouponAmountWhenNativeAmountIsUnknown()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 2, 1_000)],
            1_000,
            0,
            [
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 1), null,
                    new DateOnly(2026, 6, 15), valueInRub: 75),
                Coupon(new DateOnly(2026, 12, 31), new DateOnly(2026, 7, 1), null,
                    new DateOnly(2026, 12, 15))
            ],
            asOf: new DateOnly(2026, 7, 1),
            nextCouponDate: new DateOnly(2026, 12, 31));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.EqualTo(150));
            Assert.That(result.Income.IsCouponIncomeComplete, Is.True);
        });
    }

    [Test]
    public void Calculate_CountsAnIdenticalPublishedPaymentOnlyOnce()
    {
        var repeatedPayment = Coupon(
            new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 1), 100, new DateOnly(2026, 6, 15));
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000)],
            1_000,
            0,
            [
                repeatedPayment,
                repeatedPayment,
                Coupon(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), null,
                    new DateOnly(2026, 9, 15))
            ],
            asOf: new DateOnly(2026, 7, 1),
            nextCouponDate: new DateOnly(2026, 9, 30));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.EqualTo(100));
            Assert.That(result.Income.IsCouponIncomeComplete, Is.True);
        });
    }

    [TestCase(-1d)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void Calculate_MarksHistoryIncompleteForInvalidPublishedPaymentAmount(double invalidAmount)
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000)],
            1_000,
            0,
            [
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 1), invalidAmount,
                    new DateOnly(2026, 6, 15)),
                Coupon(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), null,
                    new DateOnly(2026, 9, 15))
            ],
            asOf: new DateOnly(2026, 7, 1),
            nextCouponDate: new DateOnly(2026, 9, 30));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Valuation.UnrealizedPnl, Is.Zero);
            Assert.That(result.Income!.HistoricalCouponIncome, Is.Zero);
            Assert.That(result.Income.IsCouponIncomeComplete, Is.False);
        });
    }

    [Test]
    public void Calculate_DoesNotDoubleCountConflictingPaymentsAndMarksHistoryIncomplete()
    {
        var paymentDate = new DateOnly(2026, 6, 30);
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000)],
            1_000,
            0,
            [
                Coupon(paymentDate, new DateOnly(2026, 1, 1), 50, new DateOnly(2026, 6, 15)),
                Coupon(paymentDate, new DateOnly(2026, 1, 1), 60, new DateOnly(2026, 6, 15)),
                Coupon(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), null,
                    new DateOnly(2026, 9, 15))
            ],
            asOf: new DateOnly(2026, 7, 2),
            nextCouponDate: new DateOnly(2026, 9, 30));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.Zero);
            Assert.That(result.Income.IsCouponIncomeComplete, Is.False);
        });
    }

    [Test]
    public void Calculate_IgnoresScheduleGapsFromBeforeTheEarliestOpenPurchase()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 4, 1), TradeSide.Buy, 1, 1_000)],
            1_000,
            0,
            [
                Coupon(new DateOnly(2025, 3, 31), new DateOnly(2024, 10, 1), 50,
                    new DateOnly(2025, 3, 15)),
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 4, 1), 100,
                    new DateOnly(2026, 6, 15)),
                Coupon(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), null,
                    new DateOnly(2026, 9, 15))
            ],
            asOf: new DateOnly(2026, 7, 2),
            nextCouponDate: new DateOnly(2026, 9, 30));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.EqualTo(100));
            Assert.That(result.Income.IsCouponIncomeComplete, Is.True);
        });
    }

    [Test]
    public void Calculate_MarksScheduleIncompleteWhenFirstPeriodStartsAfterAnOpenPurchase()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000)],
            1_000,
            0,
            [
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 5, 1), 100,
                    new DateOnly(2026, 6, 15)),
                Coupon(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), null,
                    new DateOnly(2026, 9, 15))
            ],
            asOf: new DateOnly(2026, 7, 2),
            nextCouponDate: new DateOnly(2026, 9, 30));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Valuation.UnrealizedPnl, Is.Zero);
            Assert.That(result.Income!.IsCouponIncomeComplete, Is.False);
            Assert.That(result.Income.AnnualizedTotalPnlPercent, Is.Null);
        });
    }

    [Test]
    public void Calculate_MarksScheduleIncompleteWhenAMiddleCouponPeriodIsMissing()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000)],
            1_000,
            0,
            [
                Coupon(new DateOnly(2026, 3, 31), new DateOnly(2026, 1, 1), 50,
                    new DateOnly(2026, 3, 15)),
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 4, 1), 50,
                    new DateOnly(2026, 6, 15)),
                Coupon(new DateOnly(2026, 12, 31), new DateOnly(2026, 10, 1), 50,
                    new DateOnly(2026, 12, 15)),
                Coupon(new DateOnly(2027, 3, 31), new DateOnly(2027, 1, 1), null,
                    new DateOnly(2027, 3, 15))
            ],
            asOf: new DateOnly(2026, 12, 31),
            nextCouponDate: new DateOnly(2027, 3, 31));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.EqualTo(150));
            Assert.That(result.Income.IsCouponIncomeComplete, Is.False);
            Assert.That(result.Income.AnnualizedTotalPnlPercent, Is.Null);
        });
    }

    [Test]
    public void Calculate_MarksScheduleIncompleteWhenTheLastExpectedCouponIsMissing()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000)],
            1_000,
            0,
            [
                Coupon(new DateOnly(2026, 3, 31), new DateOnly(2026, 1, 1), 100,
                    new DateOnly(2026, 3, 15)),
                Coupon(new DateOnly(2026, 12, 31), new DateOnly(2026, 10, 1), null,
                    new DateOnly(2026, 12, 15))
            ],
            asOf: new DateOnly(2026, 9, 30),
            nextCouponDate: new DateOnly(2026, 12, 31));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Valuation.UnrealizedPnl, Is.Zero);
            Assert.That(result.Income!.IsCouponIncomeComplete, Is.False);
        });
    }

    [Test]
    public void Calculate_MarksEmptyScheduleIncompleteWhenTheNextCouponDateIsKnown()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000)],
            950,
            5,
            [],
            asOf: new DateOnly(2026, 9, 30),
            nextCouponDate: new DateOnly(2026, 12, 31));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Valuation.UnrealizedPnl, Is.EqualTo(-50));
            Assert.That(result.Income!.IsCouponIncomeComplete, Is.False);
            Assert.That(result.Income.AnnualizedTotalPnlPercent, Is.Null);
        });
    }

    [TestCase(2026, 4, 24)]
    [TestCase(2026, 4, 25)]
    public void Calculate_HidesCouponMetricsWhenTradeDateIsAtRecordDateBoundary(
        int year,
        int month,
        int day)
    {
        var input = Input(
            [Trade(new DateOnly(year, month, day), TradeSide.Buy, 1, 1_000)],
            1_000,
            0,
            [
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 4, 1), 100,
                    new DateOnly(2026, 4, 25)),
                Coupon(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), null,
                    new DateOnly(2026, 9, 15))
            ],
            asOf: new DateOnly(2026, 6, 30),
            nextCouponDate: new DateOnly(2026, 9, 30));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.Zero);
            Assert.That(result.Income.IsCouponIncomeComplete, Is.False);
            Assert.That(result.Valuation.UnrealizedPnl, Is.Zero);
        });
    }

    [Test]
    public void Calculate_AttributesCouponWhenTradeDateIsMoreThanOneCalendarDayBeforeRecordDate()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 4, 23), TradeSide.Buy, 1, 1_000)],
            1_000,
            0,
            [
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 4, 1), 100,
                    new DateOnly(2026, 4, 25)),
                Coupon(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), null,
                    new DateOnly(2026, 9, 15))
            ],
            asOf: new DateOnly(2026, 6, 30),
            nextCouponDate: new DateOnly(2026, 9, 30));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.EqualTo(100));
            Assert.That(result.Income.IsCouponIncomeComplete, Is.True);
        });
    }

    [Test]
    public void Calculate_UsesThePublishedScheduleWhenCouponPeriodsHaveDifferentLengths()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000)],
            1_000,
            0,
            [
                Coupon(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1), 10,
                    new DateOnly(2026, 1, 30)),
                Coupon(new DateOnly(2026, 3, 4), new DateOnly(2026, 2, 2), 20,
                    new DateOnly(2026, 3, 3)),
                Coupon(new DateOnly(2026, 4, 15), new DateOnly(2026, 3, 5), 30,
                    new DateOnly(2026, 4, 14)),
                Coupon(new DateOnly(2026, 5, 15), new DateOnly(2026, 4, 16), null,
                    new DateOnly(2026, 5, 14))
            ],
            asOf: new DateOnly(2026, 4, 15),
            nextCouponDate: new DateOnly(2026, 5, 15));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.EqualTo(60));
            Assert.That(result.Income.IsCouponIncomeComplete, Is.True);
        });
    }

    [Test]
    public void Calculate_AnnualizesToTheCouponValuationDateInsteadOfTheRequestDate()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000)],
            1_000,
            0,
            [
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 1), 100,
                    new DateOnly(2026, 6, 15)),
                Coupon(new DateOnly(2026, 12, 31), new DateOnly(2026, 7, 1), null,
                    new DateOnly(2026, 12, 15))
            ],
            asOf: new DateOnly(2027, 1, 1),
            nextCouponDate: new DateOnly(2026, 12, 31)) with
        {
            CouponValuationDate = new DateOnly(2026, 7, 2),
            CouponFrequency = 2
        };

        var result = new FloaterPositionCalculator().Calculate(input);
        var holdingDays = new DateOnly(2026, 7, 2).DayNumber - new DateOnly(2026, 1, 1).DayNumber;

        Assert.That(
            result.Income!.AnnualizedTotalPnlPercent,
            Is.EqualTo(100 * 365d / holdingDays / 1_000 * 100).Within(0.000001));
    }

    [Test]
    public void Calculate_AnnualizesEachOpenLotUsingItsOwnHoldingPeriodAndReturn()
    {
        var asOf = new DateOnly(2027, 1, 1);
        var input = Input(
            [
                Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000, accruedInterest: 50),
                Trade(new DateOnly(2026, 7, 3), TradeSide.Buy, 1, 1_000)
            ],
            1_000,
            50,
            [
                Coupon(new DateOnly(2026, 3, 31), new DateOnly(2026, 1, 1), 100,
                    new DateOnly(2026, 3, 15)),
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 4, 1), 0,
                    new DateOnly(2026, 6, 15)),
                Coupon(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), 0,
                    new DateOnly(2026, 9, 15)),
                Coupon(new DateOnly(2026, 12, 31), new DateOnly(2026, 10, 1), 0,
                    new DateOnly(2026, 12, 15)),
                Coupon(new DateOnly(2027, 3, 31), new DateOnly(2027, 1, 1), null,
                    new DateOnly(2027, 3, 15))
            ],
            asOf,
            new DateOnly(2027, 3, 31));

        var result = new FloaterPositionCalculator().Calculate(input);
        var expectedAnnualizedPercent = 100 * (100 + 50 * 365d / 182) / 2_000;

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.EqualTo(100));
            Assert.That(result.Income.CouponIncome, Is.EqualTo(150));
            Assert.That(result.Income.AnnualizedTotalPnlPercent,
                Is.EqualTo(expectedAnnualizedPercent).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_UsesOneDayForAnnualizationWhenBoughtOnTheValuationDate()
    {
        var valuationDate = new DateOnly(2026, 12, 31);
        var input = Input(
            [Trade(valuationDate, TradeSide.Buy, 1, 1_000, accruedInterest: 5)],
            1_010,
            5,
            [Coupon(new DateOnly(2027, 3, 31), new DateOnly(2027, 1, 1), null,
                new DateOnly(2027, 3, 15))],
            asOf: valuationDate,
            nextCouponDate: new DateOnly(2027, 3, 31));

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.TotalPnl, Is.EqualTo(10));
            Assert.That(result.Income.AnnualizedTotalPnlPercent, Is.EqualTo(365));
            Assert.That(result.Income.IsCouponIncomeComplete, Is.True);
        });
    }

    [Test]
    public void Calculate_PreservesCleanPnlButMarksCouponPnlIncompleteWhenLotIsNewerThanQuote()
    {
        var input = Input(
            [
                Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000),
                Trade(new DateOnly(2026, 7, 10), TradeSide.Buy, 1, 1_100)
            ],
            950,
            7,
            [
                Coupon(new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 1), 100,
                    new DateOnly(2026, 6, 15)),
                Coupon(new DateOnly(2026, 12, 31), new DateOnly(2026, 7, 1), null,
                    new DateOnly(2026, 12, 15))
            ],
            asOf: new DateOnly(2026, 8, 1),
            nextCouponDate: new DateOnly(2026, 12, 31)) with
        {
            CouponFrequency = 2,
            CouponValuationDate = new DateOnly(2026, 7, 2)
        };

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.RemainingQuantity, Is.EqualTo(2));
            Assert.That(result.Valuation.UnrealizedPnl, Is.EqualTo(-200));
            Assert.That(result.Income.IsCouponIncomeComplete, Is.False);
            Assert.That(result.Income.AnnualizedTotalPnlPercent, Is.Null);
        });
    }

    [Test]
    public void Calculate_PreservesCleanPnlWhenCurrentAccruedInterestIsMissing()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000)],
            950,
            0,
            [],
            asOf: new DateOnly(2026, 8, 1)) with
        {
            CurrentAccruedInterest = null
        };

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.AverageBuyPriceAtCurrentFaceValue, Is.EqualTo(1_000));
            Assert.That(result.Valuation.UnrealizedPnl, Is.EqualTo(-50));
            Assert.That(result.Income, Is.Null);
        });
    }

    [TestCase(-1d)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void Calculate_PreservesCleanPnlWhenCurrentAccruedInterestIsInvalid(double invalidAccruedInterest)
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000)],
            950,
            0,
            [],
            asOf: new DateOnly(2026, 8, 1)) with
        {
            CurrentAccruedInterest = invalidAccruedInterest
        };

        var result = new FloaterPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.AverageBuyPriceAtCurrentFaceValue, Is.EqualTo(1_000));
            Assert.That(result.Valuation.UnrealizedPnl, Is.EqualTo(-50));
            Assert.That(result.Income, Is.Null);
        });
    }

    private static PositionCalculationInput Input(
        IReadOnlyCollection<PositionIncomeTrade> trades,
        double currentCleanPrice,
        double currentAccruedInterest,
        IReadOnlyCollection<PositionCouponPayment> couponPayments,
        DateOnly? asOf = null,
        DateOnly? nextCouponDate = null)
    {
        var position = new PortfolioPositionRecord(
            "FLOAT", "TQBR", "RUB", trades.Sum(x => x.Side == TradeSide.Buy ? x.Quantity : -x.Quantity),
            trades.Where(x => x.Side == TradeSide.Buy).Sum(x => x.Quantity),
            trades.Where(x => x.Side == TradeSide.Sell).Sum(x => x.Quantity),
            trades.Where(x => x.Side == TradeSide.Buy).Sum(x => x.Quantity * x.CleanPrice),
            0, 0, null, FaceUnit: "RUB");

        return new PositionCalculationInput(
            position,
            trades,
            asOf ?? AsOf,
            currentCleanPrice,
            0,
            182,
            false,
            currentAccruedInterest,
            couponPayments,
            NextCouponDate: nextCouponDate);
    }

    private static PositionIncomeTrade Trade(
        DateOnly date,
        TradeSide side,
        double quantity,
        double cleanPrice,
        double accruedInterest = 0,
        double commission = 0) =>
        new(date, side, quantity, cleanPrice, commission, null, cleanPrice, accruedInterest);

    private static PositionCouponPayment Coupon(
        DateOnly paymentDate,
        DateOnly periodStartDate,
        double? amount,
        DateOnly? recordDate,
        double? valueInRub = null) =>
        new(paymentDate, periodStartDate, amount, "RUB", 1, recordDate, valueInRub);
}
