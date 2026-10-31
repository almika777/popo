using NUnit.Framework;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;

namespace Popo.Tests;

[TestFixture]
public sealed class ZoPositionCalculatorTests
{
    private static readonly DateOnly AsOf = new(2026, 10, 5);

    [Test]
    public void TcsPosition_UsesActualRublePurchaseCostAndReportsLoss()
    {
        var input = Input(
            [
                Trade(new DateOnly(2024, 5, 17), TradeSide.Buy, 2, 91_800),
                Trade(new DateOnly(2024, 6, 18), TradeSide.Buy, 1, 89_100),
                Trade(new DateOnly(2026, 9, 15), TradeSide.Buy, 1, 80_966),
                Trade(new DateOnly(2026, 10, 2), TradeSide.Buy, 1, 80_798.3182)
            ],
            currentFaceValue: 1_000,
            marketPricePercent: 99.1707,
            faceCurrencyRateToRub: 83.4839);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.AverageBuyPriceAtCurrentFaceValue, Is.EqualTo(86_892.86364).Within(0.00001));
            Assert.That(result.Valuation.MarketValue, Is.EqualTo(413_957.8400865).Within(0.001));
            Assert.That(result.Valuation.UnrealizedPnl, Is.EqualTo(-20_506.4781135).Within(0.001));
            Assert.That(result.Valuation.UnrealizedPnlPercent, Is.EqualTo(-4.7199452877).Within(0.000001));
        });
    }

    [Test]
    public void Calculator_AnnualizesTotalPnlOverCapitalIncludingPaidAccruedInterestAndCommission()
    {
        var tradeDate = new DateOnly(2026, 10, 5);
        var input = Input(
            [Trade(tradeDate, TradeSide.Buy, 1, 1_000, accruedInterest: 50, commission: 10)],
            currentFaceValue: 1_000,
            marketPricePercent: 101.2,
            faceCurrencyRateToRub: 1,
            currentAccruedInterest: 50,
            couponPayments:
            [
                new PositionCouponPayment(new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 1),
                    100, "USD", 1)
            ],
            couponPeriodDays: 182,
            asOf: tradeDate,
            couponFrequency: 2);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.TotalPnl, Is.EqualTo(2).Within(0.001));
            Assert.That(result.Income.AnnualizedTotalPnlPercent,
                Is.EqualTo(2 * 365d / 1_060 * 100).Within(0.001));
            Assert.That(result.Income.AnnualizedReturnBreakdown!.AnnualizedPercent,
                Is.EqualTo(result.Income.AnnualizedTotalPnlPercent).Within(0.001));
            Assert.That(result.Income.AnnualizedReturnBreakdown.InvestedCapitalDays, Is.EqualTo(1_060).Within(0.001));
            Assert.That(result.Income.AnnualizedReturnBreakdown.Lots.Single().PaidAccruedInterestPerUnit, Is.EqualTo(50));
        });
    }

    [Test]
    public void DefaultCalculator_DoesNotAnnualizeAgainstQuoteOlderThanAnOpenPurchase()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 10, 5), TradeSide.Buy, 1, 1_000)],
            currentFaceValue: 1_000,
            marketPricePercent: 101.2,
            faceCurrencyRateToRub: 1,
            asOf: new DateOnly(2026, 10, 5),
            couponFrequency: 0) with
        {
            CouponValuationDate = new DateOnly(2026, 10, 4)
        };

        var result = new DefaultPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.TotalPnl, Is.EqualTo(12).Within(0.001));
            Assert.That(result.Income.AnnualizedTotalPnlPercent, Is.Null);
            Assert.That(result.Income.AnnualizedReturnBreakdown, Is.Null);
        });
    }

    [Test]
    public void DefaultCalculator_AnnualizesPnlAndHoldingDaysThroughAsOfWhenQuoteIsOlder()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 10, 1), TradeSide.Buy, 1, 1_000)],
            currentFaceValue: 1_000,
            marketPricePercent: 100.1,
            faceCurrencyRateToRub: 1,
            couponPeriodDays: 182,
            couponValue: 182,
            asOf: new DateOnly(2026, 10, 6)) with
        {
            CouponValuationDate = new DateOnly(2026, 10, 5)
        };

        var result = new DefaultPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.TotalPnl, Is.EqualTo(6).Within(0.001));
            Assert.That(result.Income.AnnualizedTotalPnlPercent,
                Is.EqualTo(6 * 365d / (1_000 * 5) * 100).Within(0.001));
            Assert.That(result.Income.AnnualizedReturnBreakdown!.ValuationDate,
                Is.EqualTo(new DateOnly(2026, 10, 6)));
        });
    }

    [Test]
    public void ZoCalculator_AnnualizesPnlAndHoldingDaysThroughAsOfWhenQuoteIsOlder()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 10, 1), TradeSide.Buy, 1, 1_000)],
            currentFaceValue: 1_000,
            marketPricePercent: 100,
            faceCurrencyRateToRub: 1,
            couponPayments:
            [
                new PositionCouponPayment(new DateOnly(2026, 4, 7), new DateOnly(2025, 10, 7),
                    100, "USD", 1, new DateOnly(2026, 4, 5)),
                new PositionCouponPayment(new DateOnly(2026, 10, 6), new DateOnly(2026, 4, 8),
                    100, "USD", 1, new DateOnly(2026, 10, 4))
            ],
            couponPeriodDays: 182,
            couponValue: 100,
            asOf: new DateOnly(2026, 10, 6),
            couponFrequency: 2,
            nextCouponDate: new DateOnly(2026, 10, 6)) with
        {
            CouponValuationDate = new DateOnly(2026, 10, 5)
        };

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.TotalPnl, Is.EqualTo(100).Within(0.001));
            Assert.That(result.Income.AnnualizedTotalPnlPercent,
                Is.EqualTo(100 * 365d / (1_000 * 5) * 100).Within(0.001));
        });
    }

    [Test]
    public void Factory_SelectsZoCalculatorForForeignNominalAndRubleTradingCurrency()
    {
        var factory = new PositionCalculatorFactory();

        Assert.Multiple(() =>
        {
            Assert.That(factory.Create("USD", "RUB"), Is.TypeOf<ZoPositionCalculator>());
            Assert.That(factory.Create("eur", "sur"), Is.TypeOf<ZoPositionCalculator>());
            Assert.That(factory.Create("CNY", "RUB"), Is.TypeOf<ZoPositionCalculator>());
        });
    }

    [TestCase("RUB", "RUB")]
    [TestCase("CNY", "CNY")]
    [TestCase("CHF", "RUB")]
    [TestCase("USD", "CNY")]
    public void Factory_LeavesOtherCurrencyPairsOnDefaultCalculator(string faceUnit, string currencyId)
    {
        Assert.That(new PositionCalculatorFactory().Create(faceUnit, currencyId),
            Is.TypeOf<DefaultPositionCalculator>());
    }

    [Test]
    public void Calculator_UsesWeightedAverageAndSaleDoesNotChangeIt()
    {
        var input = Input(
        [
            Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000),
            Trade(new DateOnly(2026, 2, 1), TradeSide.Buy, 1, 2_000),
            Trade(new DateOnly(2026, 3, 1), TradeSide.Sell, 1, 1_500)
        ], currentFaceValue: 1_000, marketPricePercent: 100, faceCurrencyRateToRub: 1);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.RemainingQuantity, Is.EqualTo(1));
            Assert.That(result.AverageBuyPriceAtCurrentFaceValue, Is.EqualTo(1_500));
            Assert.That(result.Valuation.UnrealizedPnl, Is.EqualTo(-500));
        });
    }

    [Test]
    public void Calculator_UsesWeightedAverageForSameDayTradesRegardlessOfInputOrder()
    {
        var date = new DateOnly(2026, 9, 1);
        var input = Input(
        [
            Trade(date, TradeSide.Sell, 1, 1_500),
            Trade(date, TradeSide.Buy, 1, 2_000),
            Trade(date, TradeSide.Buy, 1, 1_000)
        ], currentFaceValue: 1_000, marketPricePercent: 150, faceCurrencyRateToRub: 1);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.AverageBuyPriceAtCurrentFaceValue, Is.EqualTo(1_500));
            Assert.That(result.Valuation.UnrealizedPnl, Is.EqualTo(0));
        });
    }

    [Test]
    public void Calculator_CalculatesAverageWhenSameDayContainsBuysAndSales()
    {
        var date = new DateOnly(2026, 9, 1);
        var input = Input(
        [
            Trade(date, TradeSide.Buy, 2, 1_000),
            Trade(date, TradeSide.Sell, 1, 1_100)
        ], currentFaceValue: 1_000, marketPricePercent: 105, faceCurrencyRateToRub: 1);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.AverageBuyPriceAtCurrentFaceValue, Is.EqualTo(1_000));
            Assert.That(result.Valuation.MarketValue, Is.EqualTo(1_050));
            Assert.That(result.Valuation.UnrealizedPnl, Is.EqualTo(50));
        });
    }

    [Test]
    public void Calculator_UsesWeightedAverageAcrossSameDayBuysAfterSale()
    {
        var buyDate = new DateOnly(2026, 9, 1);
        var input = Input(
        [
            Trade(buyDate, TradeSide.Buy, 1, 800),
            Trade(buyDate, TradeSide.Buy, 1, 1_000),
            Trade(buyDate.AddDays(1), TradeSide.Sell, 1, 900)
        ], currentFaceValue: 1_000, marketPricePercent: 90, faceCurrencyRateToRub: 1);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.AverageBuyPriceAtCurrentFaceValue, Is.EqualTo(900));
            Assert.That(result.Valuation.UnrealizedPnl, Is.EqualTo(0));
        });
    }

    [Test]
    public void Calculator_PreservesRemainingAveragePriceWhenCurrentQuoteIsUnavailable()
    {
        var input = Input(
        [
            Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000),
            Trade(new DateOnly(2026, 2, 1), TradeSide.Buy, 1, 2_000),
            Trade(new DateOnly(2026, 3, 1), TradeSide.Sell, 1, 1_500)
        ], currentFaceValue: 1_000, marketPricePercent: 100, faceCurrencyRateToRub: null);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.That(result.AverageBuyPriceAtCurrentFaceValue, Is.EqualTo(1_500));
    }

    [Test]
    public void Calculator_UsesEachHistoricalCouponAmountAndItsOwnExchangeRate()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 10, 1_000)],
            currentFaceValue: 1_000,
            marketPricePercent: 100,
            faceCurrencyRateToRub: 1,
            couponPayments:
            [
                new PositionCouponPayment(new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 2),
                    100, "USD", 90),
                new PositionCouponPayment(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 2),
                    200, "USD", 95)
            ],
            couponValue: 200,
            couponPeriodDays: 182);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.CouponIncome, Is.EqualTo(280_000));
            Assert.That(result.Income.IsCouponIncomeComplete, Is.True);
        });
    }

    [Test]
    public void Calculator_UsesRecordDateForCouponEntitlementAndPublishedRubleAmount()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 3, 1), TradeSide.Buy, 1, 1_000)],
            currentFaceValue: 1_000,
            marketPricePercent: 100,
            faceCurrencyRateToRub: 1,
            couponPayments:
            [
                new PositionCouponPayment(
                    new DateOnly(2026, 6, 30),
                    new DateOnly(2026, 1, 2),
                    100,
                    "USD",
                    null,
                    new DateOnly(2026, 6, 15),
                    110)
            ],
            couponValue: 100,
            couponPeriodDays: 182);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.EqualTo(110));
            Assert.That(result.Income.IsCouponIncomeComplete, Is.True);
        });
    }

    [Test]
    public void Calculator_AllocatesCouponsOnlyToLotsHeldOnEachPaymentDate()
    {
        var input = Input(
            [
                Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000),
                Trade(new DateOnly(2026, 7, 1), TradeSide.Buy, 1, 1_000)
            ],
            currentFaceValue: 1_000,
            marketPricePercent: 100,
            faceCurrencyRateToRub: 1,
            couponPayments:
            [
                new PositionCouponPayment(new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 2),
                    100, "USD", 1),
                new PositionCouponPayment(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 2),
                    200, "USD", 1)
            ],
            couponValue: 200,
            couponPeriodDays: 91);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.CouponIncome, Is.EqualTo(500));
            Assert.That(result.Income.IsCouponIncomeComplete, Is.True);
        });
    }

    [Test]
    public void Calculator_NetsPurchaseAccruedInterestAgainstCouponPaymentsAndCurrentAccruedInterest()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000, accruedInterest: 30)],
            currentFaceValue: 1_000,
            marketPricePercent: 100,
            faceCurrencyRateToRub: 1,
            currentAccruedInterest: 40,
            couponPayments: [new PositionCouponPayment(new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 2),
                100, "USD", 1)],
            couponValue: 100,
            couponPeriodDays: 182);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.CouponIncome, Is.EqualTo(110));
            Assert.That(result.Income!.TotalPnl, Is.EqualTo(110));
        });
    }

    [Test]
    public void Calculator_ExcludesPaymentsBeforePurchaseAndAfterValuationDate()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 6, 30), TradeSide.Buy, 1, 1_000)],
            currentFaceValue: 1_000,
            marketPricePercent: 100,
            faceCurrencyRateToRub: 1,
            couponPayments:
            [
                new PositionCouponPayment(new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 2),
                    100, "USD", 1),
                new PositionCouponPayment(new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1),
                    200, "USD", 1),
                new PositionCouponPayment(new DateOnly(2026, 12, 31), new DateOnly(2026, 10, 1),
                    300, "USD", 1)
            ],
            couponValue: 200,
            couponPeriodDays: 182);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.That(result.Income!.CouponIncome, Is.EqualTo(200));
    }

    [Test]
    public void Calculator_MarksCouponEstimateIncompleteForPurchasesInsideCouponPeriod()
    {
        var input = Input(
            [
                Trade(new DateOnly(2026, 1, 31), TradeSide.Buy, 1, 1_000),
                Trade(new DateOnly(2026, 3, 1), TradeSide.Buy, 1, 1_000)
            ],
            currentFaceValue: 1_000,
            marketPricePercent: 100,
            faceCurrencyRateToRub: 1,
            couponPayments: [new PositionCouponPayment(new DateOnly(2026, 6, 30), new DateOnly(2026, 2, 1),
                100, "USD", 1)],
            couponValue: 100,
            couponPeriodDays: 182);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.EqualTo(100));
            Assert.That(result.Income.IsCouponIncomeComplete, Is.False);
            Assert.That(result.Valuation.UnrealizedPnl, Is.Zero);
        });
    }

    [Test]
    public void Calculator_MarksCouponEstimateIncompleteWhenHistoricalRateIsUnavailable()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000)],
            currentFaceValue: 1_000,
            marketPricePercent: 100,
            faceCurrencyRateToRub: 1,
            couponPayments: [new PositionCouponPayment(new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 2),
                100, "USD", null)],
            couponValue: 100,
            couponPeriodDays: 182);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.HistoricalCouponIncome, Is.Zero);
            Assert.That(result.Income.IsCouponIncomeComplete, Is.False);
            Assert.That(result.Valuation.UnrealizedPnl, Is.Zero);
        });
    }

    [Test]
    public void Calculator_MarksCouponEstimateIncompleteWhenHistoryHasNoExpectedPayments()
    {
        var input = Input(
            [Trade(new DateOnly(2025, 1, 1), TradeSide.Buy, 1, 1_000)],
            currentFaceValue: 1_000,
            marketPricePercent: 100,
            faceCurrencyRateToRub: 1,
            couponValue: 100,
            couponPeriodDays: 182);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.That(result.Income!.IsCouponIncomeComplete, Is.False);
    }

    [Test]
    public void Calculator_MarksCouponHistoryIncompleteWhenExpectedPaymentIsMissingAfterPurchase()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 2, 1), TradeSide.Buy, 1, 1_000)],
            currentFaceValue: 1_000,
            marketPricePercent: 100,
            faceCurrencyRateToRub: 1,
            couponValue: 100,
            couponPeriodDays: 182,
            asOf: new DateOnly(2026, 7, 1),
            couponPayments:
            [
                new PositionCouponPayment(new DateOnly(2025, 12, 31), new DateOnly(2025, 7, 2),
                    100, "USD", 1)
            ]);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.That(result.Income!.IsCouponIncomeComplete, Is.False);
    }

    [Test]
    public void Calculator_MarksRecentPositionIncompleteWhenScheduledCouponIsMissing()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 9, 1), TradeSide.Buy, 1, 1_000)],
            currentFaceValue: 1_000,
            marketPricePercent: 100,
            faceCurrencyRateToRub: 1,
            currentAccruedInterest: 10,
            couponValue: 100,
            couponPeriodDays: 91,
            nextCouponDate: new DateOnly(2026, 12, 15));

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Income!.IsCouponIncomeComplete, Is.False);
            Assert.That(result.Income.CouponIncome, Is.EqualTo(10));
        });
    }

    [Test]
    public void Calculator_MarksCouponEstimateIncompleteWhenPublishedPeriodsHaveAGap()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000)],
            currentFaceValue: 1_000,
            marketPricePercent: 100,
            faceCurrencyRateToRub: 1,
            couponValue: 100,
            couponPeriodDays: 182,
            asOf: new DateOnly(2027, 10, 5),
            couponPayments:
            [
                new PositionCouponPayment(new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 2),
                    100, "USD", 1),
                new PositionCouponPayment(new DateOnly(2027, 6, 30), new DateOnly(2027, 1, 1),
                    100, "USD", 1)
            ]);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.That(result.Income!.IsCouponIncomeComplete, Is.False);
    }

    [TestCase(181, true)]
    [TestCase(182, false)]
    public void Calculator_MarksHistoryIncompleteWhenExpectedPaymentDateIsReached(
        int holdingDays,
        bool expectedComplete)
    {
        var tradeDate = new DateOnly(2026, 1, 1);
        var input = Input(
            [Trade(tradeDate, TradeSide.Buy, 1, 1_000)],
            currentFaceValue: 1_000,
            marketPricePercent: 100,
            faceCurrencyRateToRub: 1,
            couponValue: 100,
            couponPeriodDays: 182,
            couponFrequency: 2,
            asOf: tradeDate.AddDays(holdingDays));

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.That(result.Income!.IsCouponIncomeComplete, Is.EqualTo(expectedComplete));
    }

    [Test]
    public void Calculator_MarksEmptyCouponMetadataIncompleteForAnOldPosition()
    {
        var input = Input(
            [Trade(new DateOnly(2025, 1, 1), TradeSide.Buy, 1, 1_000)],
            currentFaceValue: 1_000,
            marketPricePercent: 100,
            faceCurrencyRateToRub: 1);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.That(result.Income!.IsCouponIncomeComplete, Is.False);
    }

    [Test]
    public void Calculator_KeepsEmptyHistoryCompleteForExplicitZeroCouponInstrument()
    {
        var input = Input(
            [Trade(new DateOnly(2025, 1, 1), TradeSide.Buy, 1, 1_000)],
            currentFaceValue: 1_000,
            marketPricePercent: 100,
            faceCurrencyRateToRub: 1,
            couponFrequency: 0);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.That(result.Income!.IsCouponIncomeComplete, Is.True);
    }

    [Test]
    public void Calculator_ReturnsNoMarketResultWhenCurrentExchangeRateIsMissing()
    {
        var input = Input(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000)],
            currentFaceValue: 1_000,
            marketPricePercent: 100,
            faceCurrencyRateToRub: null);

        var result = new ZoPositionCalculator().Calculate(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Valuation.MarketValue, Is.Null);
            Assert.That(result.Valuation.UnrealizedPnl, Is.Null);
        });
    }

    private static PositionCalculationInput Input(
        IReadOnlyCollection<PortfolioTradeRecord> trades,
        double? currentFaceValue,
        double? marketPricePercent,
        double? faceCurrencyRateToRub,
        double currentAccruedInterest = 0,
        IReadOnlyCollection<PositionCouponPayment>? couponPayments = null,
        int couponPeriodDays = 0,
        double couponValue = 0,
        DateOnly? asOf = null,
        int? couponFrequency = null,
        DateOnly? nextCouponDate = null)
    {
        var incomeTrades = trades.Select(x => new PositionIncomeTrade(
            x.TradeDate,
            x.Side,
            x.Quantity,
            x.Price,
            x.Commission,
            null,
            x.Price,
            x.AccruedInterest)).ToArray();
        var boughtQuantity = trades.Where(x => x.Side == TradeSide.Buy).Sum(x => x.Quantity);
        var soldQuantity = trades.Where(x => x.Side == TradeSide.Sell).Sum(x => x.Quantity);
        var boughtCleanAmount = trades.Where(x => x.Side == TradeSide.Buy).Sum(x => x.Quantity * x.Price);
        var position = new PortfolioPositionRecord(
            "RU000A107738",
            "TQCB",
            "RUB",
            boughtQuantity - soldQuantity,
            boughtQuantity,
            soldQuantity,
            boughtCleanAmount,
            boughtCleanAmount,
            0,
            boughtQuantity == 0 ? 0 : boughtCleanAmount / boughtQuantity,
            FaceUnit: "USD");
        double? currentCleanPrice = currentFaceValue.HasValue && marketPricePercent.HasValue
                                    && faceCurrencyRateToRub.HasValue
            ? currentFaceValue.Value * marketPricePercent.Value / 100 * faceCurrencyRateToRub.Value
            : null;

        return new PositionCalculationInput(
            position,
            incomeTrades,
            asOf ?? AsOf,
            currentCleanPrice,
            couponValue,
            couponPeriodDays,
            false,
            currentAccruedInterest,
            couponPayments ?? [],
            couponFrequency,
            nextCouponDate);
    }

    private static PortfolioTradeRecord Trade(
        DateOnly date,
        TradeSide side,
        double quantity,
        double price,
        double tradeFaceValue = 1_000,
        double accruedInterest = 0,
        double commission = 0) =>
        new(Guid.NewGuid(), "RU000A107738", "TQCB", "RUB", date, side, quantity, price,
            tradeFaceValue, accruedInterest, commission, 0, quantity * price + commission);
}
