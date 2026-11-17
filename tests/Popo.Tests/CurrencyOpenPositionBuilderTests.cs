using NUnit.Framework;
using Popo.Api.Services.Position;
using Popo.Api.Services.Position.Builders;
using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;

namespace Popo.Tests;

[TestFixture]
public sealed class CurrencyOpenPositionBuilderTests
{
    [TestCaseSource(nameof(OpenPositionCases))]
    public void Build_CalculatesOpenPosition(Scenario scenario)
    {
        var result = new CurrencyOpenPositionBuilder().Build(scenario.Input);
        var expected = scenario.Expected;

        Assert.Multiple(() =>
        {
            Assert.That(result.Quantity, Is.EqualTo(expected.Quantity));
            Assert.That(result.AvgBuyPrice, Is.EqualTo(expected.AvgBuyPrice).Within(0.000001));
            Assert.That(result.MarketPrice, Is.EqualTo(expected.MarketPrice).Within(0.000001));
            Assert.That(result.TotalCost, Is.EqualTo(expected.TotalCost).Within(0.000001));
            Assert.That(result.UnrealizedPnl, Is.EqualTo(expected.UnrealizedPnl).Within(0.000001));
            Assert.That(result.CouponIncome, Is.EqualTo(expected.CouponIncome).Within(0.000001));
            Assert.That(result.TotalPnl, Is.EqualTo(expected.TotalPnl).Within(0.000001));
        });
    }

    private static IEnumerable<TestCaseData> OpenPositionCases()
    {
        var inputWithOnePurchase = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-ONE", "TQCB", "RUB", new DateOnly(2026, 1, 11),
                    TradeSide.Buy, 10, 85_000, 1_000, 0, 1_000, 1000d / 850_000 * 100, 851_000,
                    "Валютная облигация")
            ],
            Security = new MoexBond
            {
                SecId = "CURRENCY-ONE", BoardId = "TQCB", ShortName = "Валютная облигация",
                FaceValue = 1_000, FaceUnit = "USD", BondType = "Валютная облигация"
            },
            Marketdata = new MoexMarketdata { SecId = "CURRENCY-ONE", BoardId = "TQCB", LCurrentPrice = 99 },
            AsOf = new DateOnly(2026, 1, 20),
            Coupons =
            [
                new OpenPositionCoupon("CURRENCY-ONE", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
                    30, "USD")
            ],
            CurrentCurrencyRate = new PortfolioCurrencyRateRecord("USD", new DateOnly(2026, 1, 20), 90)
        };

        yield return new TestCaseData(new Scenario(inputWithOnePurchase,
                new ExpectedResult(10, 85_000, 89_100, 891_000, 41_000, 9_000, 49_000)))
            .SetName("Build_OnePurchaseWithCurrentCouponInRubles");

        var input = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "REPLACEMENT", "TQCB", "RUB", new DateOnly(2026, 1, 21),
                    TradeSide.Buy, 10, 8_500, 100, 0, 100, 0, 85_100, "Замещающая облигация")
            ],
            Security = new MoexBond
            {
                SecId = "REPLACEMENT", BoardId = "TQCB", ShortName = "Замещающая облигация",
                FaceValue = 100, FaceUnit = "USD", BondType = "Валютная облигация"
            },
            Marketdata = new MoexMarketdata { SecId = "REPLACEMENT", BoardId = "TQCB", LCurrentPrice = 100 },
            AsOf = new DateOnly(2026, 2, 9),
            Coupons =
            [
                new OpenPositionCoupon("REPLACEMENT", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
                    30, "USD", 2_700),
                new OpenPositionCoupon("REPLACEMENT", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2),
                    30, "USD")
            ],
            CurrentCurrencyRate = new PortfolioCurrencyRateRecord("USD", new DateOnly(2026, 2, 9), 100)
        };

        yield return new TestCaseData(new Scenario(input,
                new ExpectedResult(10, 8_500, 10_000, 100_000, 15_000, 19_000, 33_900)))
            .SetName("Build_PaidCouponAndCurrentCouponInDifferentRates");

        var inputWithMultiplePurchases = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-BUYS", "TQCB", "RUB", new DateOnly(2026, 1, 1),
                    TradeSide.Buy, 10, 80_000, 1_000, 0, 1_000, 0.125, 801_000, "Валютная облигация"),
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-BUYS", "TQCB", "RUB", new DateOnly(2026, 1, 11),
                    TradeSide.Buy, 10, 90_000, 1_000, 0, 1_000, 1000d / 900_000 * 100, 901_000, "Валютная облигация")
            ],
            Security = new MoexBond
            {
                SecId = "CURRENCY-BUYS", BoardId = "TQCB", ShortName = "Валютная облигация",
                FaceValue = 1_000, FaceUnit = "USD", BondType = "Валютная облигация"
            },
            Marketdata = new MoexMarketdata { SecId = "CURRENCY-BUYS", BoardId = "TQCB", LCurrentPrice = 100 },
            AsOf = new DateOnly(2026, 1, 20),
            Coupons = [new OpenPositionCoupon("CURRENCY-BUYS", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "USD")],
            CurrentCurrencyRate = new PortfolioCurrencyRateRecord("USD", new DateOnly(2026, 1, 20), 90)
        };
        yield return new TestCaseData(new Scenario(inputWithMultiplePurchases,
                new ExpectedResult(20, 85_000, 90_000, 1_800_000, 100_000, 27_000, 125_000)))
            .SetName("Build_MultiplePurchasesAtDifferentPrices");

        var inputWithPartialSale = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-PARTIAL", "TQCB", "RUB", new DateOnly(2026, 1, 1),
                    TradeSide.Buy, 10, 80_000, 1_000, 0, 1_000, 0.125, 801_000, "Валютная облигация"),
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-PARTIAL", "TQCB", "RUB", new DateOnly(2026, 1, 11),
                    TradeSide.Sell, 4, 100_000, 1_000, 0, 200, 0.05, 399_800, "Валютная облигация")
            ],
            Security = new MoexBond
            {
                SecId = "CURRENCY-PARTIAL", BoardId = "TQCB", ShortName = "Валютная облигация",
                FaceValue = 1_000, FaceUnit = "USD", BondType = "Валютная облигация"
            },
            Marketdata = new MoexMarketdata { SecId = "CURRENCY-PARTIAL", BoardId = "TQCB", LCurrentPrice = 100 },
            AsOf = new DateOnly(2026, 1, 20),
            Coupons = [new OpenPositionCoupon("CURRENCY-PARTIAL", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "USD")],
            CurrentCurrencyRate = new PortfolioCurrencyRateRecord("USD", new DateOnly(2026, 1, 20), 90)
        };
        yield return new TestCaseData(new Scenario(inputWithPartialSale,
                new ExpectedResult(6, 80_000, 90_000, 540_000, 60_000, 10_800, 70_200)))
            .SetName("Build_PartialSaleAfterOnePurchase");

        var inputWithPartialSaleAfterMultiplePurchases = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-BUYS-SALE", "TQCB", "RUB", new DateOnly(2026, 1, 1),
                    TradeSide.Buy, 10, 80_000, 1_000, 0, 1_000, 0.125, 801_000, "Валютная облигация"),
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-BUYS-SALE", "TQCB", "RUB", new DateOnly(2026, 1, 11),
                    TradeSide.Buy, 10, 90_000, 1_000, 0, 1_000, 1000d / 900_000 * 100, 901_000, "Валютная облигация"),
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-BUYS-SALE", "TQCB", "RUB", new DateOnly(2026, 1, 16),
                    TradeSide.Sell, 10, 95_000, 1_000, 0, 0, 0, 950_000, "Валютная облигация")
            ],
            Security = new MoexBond
            {
                SecId = "CURRENCY-BUYS-SALE", BoardId = "TQCB", ShortName = "Валютная облигация",
                FaceValue = 1_000, FaceUnit = "USD", BondType = "Валютная облигация"
            },
            Marketdata = new MoexMarketdata { SecId = "CURRENCY-BUYS-SALE", BoardId = "TQCB", LCurrentPrice = 100 },
            AsOf = new DateOnly(2026, 1, 20),
            Coupons = [new OpenPositionCoupon("CURRENCY-BUYS-SALE", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "USD")],
            CurrentCurrencyRate = new PortfolioCurrencyRateRecord("USD", new DateOnly(2026, 1, 20), 90)
        };
        yield return new TestCaseData(new Scenario(inputWithPartialSaleAfterMultiplePurchases,
                new ExpectedResult(10, 85_000, 90_000, 900_000, 50_000, 13_500, 62_500)))
            .SetName("Build_PartialSaleAfterMultiplePurchases");

        var inputWithPurchaseAfterSale = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-BUY-AGAIN", "TQCB", "RUB", new DateOnly(2026, 1, 1),
                    TradeSide.Buy, 10, 80_000, 1_000, 0, 1_000, 0.125, 801_000, "Валютная облигация"),
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-BUY-AGAIN", "TQCB", "RUB", new DateOnly(2026, 1, 11),
                    TradeSide.Sell, 4, 95_000, 1_000, 0, 0, 0, 380_000, "Валютная облигация"),
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-BUY-AGAIN", "TQCB", "RUB", new DateOnly(2026, 1, 16),
                    TradeSide.Buy, 4, 90_000, 1_000, 0, 400, 400d / 360_000 * 100, 360_400, "Валютная облигация")
            ],
            Security = new MoexBond
            {
                SecId = "CURRENCY-BUY-AGAIN", BoardId = "TQCB", ShortName = "Валютная облигация",
                FaceValue = 1_000, FaceUnit = "USD", BondType = "Валютная облигация"
            },
            Marketdata = new MoexMarketdata { SecId = "CURRENCY-BUY-AGAIN", BoardId = "TQCB", LCurrentPrice = 100 },
            AsOf = new DateOnly(2026, 1, 20),
            Coupons = [new OpenPositionCoupon("CURRENCY-BUY-AGAIN", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "USD")],
            CurrentCurrencyRate = new PortfolioCurrencyRateRecord("USD", new DateOnly(2026, 1, 20), 90)
        };
        yield return new TestCaseData(new Scenario(inputWithPurchaseAfterSale,
                new ExpectedResult(10, 84_000, 90_000, 900_000, 60_000, 12_600, 71_600)))
            .SetName("Build_PurchaseAfterPartialSale");

        var inputWithReopenedPosition = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-REOPEN", "TQCB", "RUB", new DateOnly(2026, 1, 1),
                    TradeSide.Buy, 10, 80_000, 1_000, 0, 1_000, 0.125, 801_000, "Валютная облигация"),
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-REOPEN", "TQCB", "RUB", new DateOnly(2026, 1, 10),
                    TradeSide.Sell, 10, 95_000, 1_000, 0, 0, 0, 950_000, "Валютная облигация"),
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-REOPEN", "TQCB", "RUB", new DateOnly(2026, 1, 11),
                    TradeSide.Buy, 4, 85_000, 1_000, 0, 400, 400d / 340_000 * 100, 340_400, "Валютная облигация")
            ],
            Security = new MoexBond
            {
                SecId = "CURRENCY-REOPEN", BoardId = "TQCB", ShortName = "Валютная облигация",
                FaceValue = 1_000, FaceUnit = "USD", BondType = "Валютная облигация"
            },
            Marketdata = new MoexMarketdata { SecId = "CURRENCY-REOPEN", BoardId = "TQCB", LCurrentPrice = 80 },
            AsOf = new DateOnly(2026, 1, 20),
            Coupons = [new OpenPositionCoupon("CURRENCY-REOPEN", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "USD")],
            CurrentCurrencyRate = new PortfolioCurrencyRateRecord("USD", new DateOnly(2026, 1, 20), 90)
        };
        yield return new TestCaseData(new Scenario(inputWithReopenedPosition,
                new ExpectedResult(4, 85_000, 72_000, 288_000, -52_000, 3_600, -48_800)))
            .SetName("Build_ReopenedPositionWithLoss");

        var inputWithMultiplePaidCoupons = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-PAID", "TQCB", "RUB", new DateOnly(2026, 1, 21),
                    TradeSide.Buy, 10, 8_500, 100, 0, 100, 100d / 85_000 * 100, 85_100, "Валютная облигация")
            ],
            Security = new MoexBond
            {
                SecId = "CURRENCY-PAID", BoardId = "TQCB", ShortName = "Валютная облигация",
                FaceValue = 100, FaceUnit = "USD", BondType = "Валютная облигация"
            },
            Marketdata = new MoexMarketdata { SecId = "CURRENCY-PAID", BoardId = "TQCB", LCurrentPrice = 100 },
            AsOf = new DateOnly(2026, 3, 11),
            Coupons =
            [
                new OpenPositionCoupon("CURRENCY-PAID", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "USD", 2_700),
                new OpenPositionCoupon("CURRENCY-PAID", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2), 30, "USD", 2_400),
                new OpenPositionCoupon("CURRENCY-PAID", new DateOnly(2026, 3, 2), new DateOnly(2026, 4, 1), 30, "USD")
            ],
            CurrentCurrencyRate = new PortfolioCurrencyRateRecord("USD", new DateOnly(2026, 3, 11), 100)
        };
        yield return new TestCaseData(new Scenario(inputWithMultiplePaidCoupons,
                new ExpectedResult(10, 8_500, 10_000, 100_000, 15_000, 43_000, 57_900)))
            .SetName("Build_MultiplePaidCouponsAndCurrentCoupon");

        var inputWithMultiplePartialSales = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-MULTI-SALE", "TQCB", "RUB", new DateOnly(2026, 1, 1),
                    TradeSide.Buy, 10, 80_000, 1_000, 0, 1_000, 0.125, 801_000, "Валютная облигация"),
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-MULTI-SALE", "TQCB", "RUB", new DateOnly(2026, 1, 6),
                    TradeSide.Sell, 4, 95_000, 1_000, 0, 0, 0, 380_000, "Валютная облигация"),
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-MULTI-SALE", "TQCB", "RUB", new DateOnly(2026, 1, 11),
                    TradeSide.Sell, 3, 95_000, 1_000, 0, 0, 0, 285_000, "Валютная облигация")
            ],
            Security = new MoexBond
            {
                SecId = "CURRENCY-MULTI-SALE", BoardId = "TQCB", ShortName = "Валютная облигация",
                FaceValue = 1_000, FaceUnit = "USD", BondType = "Валютная облигация"
            },
            Marketdata = new MoexMarketdata { SecId = "CURRENCY-MULTI-SALE", BoardId = "TQCB", LCurrentPrice = 100 },
            AsOf = new DateOnly(2026, 1, 20),
            Coupons = [new OpenPositionCoupon("CURRENCY-MULTI-SALE", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "USD")],
            CurrentCurrencyRate = new PortfolioCurrencyRateRecord("USD", new DateOnly(2026, 1, 20), 90)
        };
        yield return new TestCaseData(new Scenario(inputWithMultiplePartialSales,
                new ExpectedResult(3, 80_000, 90_000, 270_000, 30_000, 5_400, 35_100)))
            .SetName("Build_MultiplePartialSales");

        var inputWithPurchasesInDifferentPeriods = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-PERIOD-BUYS", "TQCB", "RUB", new DateOnly(2026, 1, 21),
                    TradeSide.Buy, 10, 8_500, 100, 0, 100, 100d / 85_000 * 100, 85_100, "Валютная облигация"),
                new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-PERIOD-BUYS", "TQCB", "RUB", new DateOnly(2026, 2, 5),
                    TradeSide.Buy, 5, 9_500, 100, 0, 50, 50d / 47_500 * 100, 47_550, "Валютная облигация")
            ],
            Security = new MoexBond
            {
                SecId = "CURRENCY-PERIOD-BUYS", BoardId = "TQCB", ShortName = "Валютная облигация",
                FaceValue = 100, FaceUnit = "USD", BondType = "Валютная облигация"
            },
            Marketdata = new MoexMarketdata { SecId = "CURRENCY-PERIOD-BUYS", BoardId = "TQCB", LCurrentPrice = 100 },
            AsOf = new DateOnly(2026, 2, 9),
            Coupons =
            [
                new OpenPositionCoupon("CURRENCY-PERIOD-BUYS", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "USD", 2_700),
                new OpenPositionCoupon("CURRENCY-PERIOD-BUYS", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2), 30, "USD")
            ],
            CurrentCurrencyRate = new PortfolioCurrencyRateRecord("USD", new DateOnly(2026, 2, 9), 100)
        };
        yield return new TestCaseData(new Scenario(inputWithPurchasesInDifferentPeriods,
                new ExpectedResult(15, 132_500d / 15, 10_000, 150_000, 17_500, 21_500, 38_850)))
            .SetName("Build_PurchasesInDifferentCouponPeriods");

        var inputWithUsdPosition = new OpenPositionBuildInput
        {
            Trades = [new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-USD", "TQOD", "USD", new DateOnly(2026, 1, 21),
                TradeSide.Buy, 10, 85, 100, 0, 1, 1d / 850 * 100, 851, "Валютная облигация")],
            Security = new MoexBond { SecId = "CURRENCY-USD", BoardId = "TQOD", ShortName = "Валютная облигация",
                FaceValue = 100, FaceUnit = "USD", BondType = "Валютная облигация" },
            Marketdata = new MoexMarketdata { SecId = "CURRENCY-USD", BoardId = "TQOD", LCurrentPrice = 100 },
            AsOf = new DateOnly(2026, 2, 9),
            Coupons =
            [
                new OpenPositionCoupon("CURRENCY-USD", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "USD", 2_700),
                new OpenPositionCoupon("CURRENCY-USD", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2), 30, "USD")
            ]
        };
        yield return new TestCaseData(new Scenario(inputWithUsdPosition,
                new ExpectedResult(10, 85, 100, 1_000, 150, 200, 349)))
            .SetName("Build_UsdPositionUsesCouponInNominalCurrency");

        var inputWithCnyPosition = new OpenPositionBuildInput
        {
            Trades = [new PortfolioTradeRecord(Guid.NewGuid(), "CURRENCY-CNY", "TQOY", "CNY", new DateOnly(2026, 1, 21),
                TradeSide.Buy, 10, 850, 100, 0, 10, 10d / 8_500 * 100, 8_510, "Валютная облигация")],
            Security = new MoexBond { SecId = "CURRENCY-CNY", BoardId = "TQOY", ShortName = "Валютная облигация",
                FaceValue = 100, FaceUnit = "USD", BondType = "Валютная облигация" },
            Marketdata = new MoexMarketdata { SecId = "CURRENCY-CNY", BoardId = "TQOY", LCurrentPrice = 100 },
            AsOf = new DateOnly(2026, 2, 9),
            Coupons =
            [
                new OpenPositionCoupon("CURRENCY-CNY", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "USD", 2_700),
                new OpenPositionCoupon("CURRENCY-CNY", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2), 30, "USD")
            ],
            CurrencyRates =
            [
                new PortfolioCurrencyRateRecord("USD", new DateOnly(2026, 2, 9), 100),
                new PortfolioCurrencyRateRecord("CNY", new DateOnly(2026, 1, 31), 9),
                new PortfolioCurrencyRateRecord("CNY", new DateOnly(2026, 2, 9), 10)
            ]
        };
        yield return new TestCaseData(new Scenario(inputWithCnyPosition,
                new ExpectedResult(10, 850, 1_000, 10_000, 1_500, 2_000, 3_490)))
            .SetName("Build_CnyPositionUsesPaymentAndCurrentRates");
    }

    public sealed record Scenario(OpenPositionBuildInput Input, ExpectedResult Expected);

    public sealed record ExpectedResult(
        int Quantity,
        double AvgBuyPrice,
        double MarketPrice,
        double TotalCost,
        double UnrealizedPnl,
        double CouponIncome,
        double TotalPnl);
}
