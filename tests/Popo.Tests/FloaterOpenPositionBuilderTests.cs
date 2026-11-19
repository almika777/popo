using NUnit.Framework;
using Popo.Api.Services.Position;
using Popo.Api.Services.Position.Builders;
using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;

namespace Popo.Tests;

[TestFixture]
public sealed class FloaterOpenPositionBuilderTests
{
    [TestCaseSource(nameof(OpenPositionCases))]
    public void Build_CalculatesOpenPosition(Scenario scenario)
    {
        var result = new FloaterOpenPositionBuilder().Build(scenario.Input);
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
        var input = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 1, 1), TradeSide.Buy,
                    10, 1_000, 1_000, 0, 20, 0, 10_020, "Флоатер")
            ],
            Security = new MoexBond
            {
                SecId = "BOND", BoardId = "TQCB", ShortName = "Флоатер", FaceValue = 1_000,
                FaceUnit = "RUB", BondType = "Облигация с плавающим купоном"
            },
            Marketdata = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 110 },
            AsOf = new DateOnly(2026, 1, 10),
            Coupons = [new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "RUB")]
        };

        yield return new TestCaseData(new Scenario(input,
                new ExpectedResult(10, 1_000, 1_100, 11_000, 1_000, 100, 1_080)))
            .SetName("Build_OnePurchaseWithoutSales");

        var inputWithCouponChange = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 1, 1), TradeSide.Buy,
                    10, 1_000, 1_000, 0, 20, 0, 10_020, "Флоатер")
            ],
            Security = new MoexBond
            {
                SecId = "BOND", BoardId = "TQCB", ShortName = "Флоатер", FaceValue = 1_000, FaceUnit = "RUB",
                BondType = "Облигация с плавающим купоном"
            },
            Marketdata = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 110 },
            AsOf = new DateOnly(2026, 2, 9),
            Coupons =
            [
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "RUB"),
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2), 60, "RUB")
            ]
        };
        yield return new TestCaseData(new Scenario(inputWithCouponChange,
                new ExpectedResult(10, 1_000, 1_100, 11_000, 1_000, 500, 1_480)))
            .SetName("Build_OnePurchaseWithCouponChange");

        var inputBoughtOnFirstDay = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 1, 31),
                    TradeSide.Buy, 10, 1_000, 1_000, 0, 20, 0, 10_020, "Флоатер")
            ],
            Security = new MoexBond
            {
                SecId = "BOND", BoardId = "TQCB", ShortName = "Флоатер", FaceValue = 1_000, FaceUnit = "RUB",
                BondType = "Облигация с плавающим купоном"
            },
            Marketdata = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 110 },
            AsOf = new DateOnly(2026, 2, 9),
            Coupons =
            [
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "RUB"),
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2), 60, "RUB")
            ]
        };
        yield return new TestCaseData(new Scenario(inputBoughtOnFirstDay,
                new ExpectedResult(10, 1_000, 1_100, 11_000, 1_000, 200, 1_180)))
            .SetName("Build_PurchaseOnFirstDayOfCouponPeriod");

        var inputBoughtOnLastDay = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 1, 30),
                    TradeSide.Buy, 10, 1_000, 1_000, 0, 20, 0, 10_020, "Флоатер")
            ],
            Security = new MoexBond
            {
                SecId = "BOND", BoardId = "TQCB", ShortName = "Флоатер", FaceValue = 1_000, FaceUnit = "RUB",
                BondType = "Облигация с плавающим купоном"
            },
            Marketdata = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 110 },
            AsOf = new DateOnly(2026, 2, 9),
            Coupons =
            [
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "RUB"),
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2), 60, "RUB")
            ]
        };
        yield return new TestCaseData(new Scenario(inputBoughtOnLastDay,
                new ExpectedResult(10, 1_000, 1_100, 11_000, 1_000, 210, 1_190)))
            .SetName("Build_PurchaseOnLastDayOfCouponPeriod");

        var inputWithUnknownCurrentCoupon = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 1, 1), TradeSide.Buy,
                    10, 1_000, 1_000, 0, 20, 0, 10_020, "Флоатер")
            ],
            Security = new MoexBond
            {
                SecId = "BOND", BoardId = "TQCB", ShortName = "Флоатер", FaceValue = 1_000, FaceUnit = "RUB",
                BondType = "Облигация с плавающим купоном"
            },
            Marketdata = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 110 },
            AsOf = new DateOnly(2026, 2, 9),
            Coupons =
            [
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "RUB"),
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2), null, "RUB")
            ]
        };
        yield return new TestCaseData(new Scenario(inputWithUnknownCurrentCoupon,
                new ExpectedResult(10, 1_000, 1_100, 11_000, 1_000, 400, 1_380)))
            .SetName("Build_UsesPreviousCouponForUnknownCurrentPeriod");

        var inputWithTwoPurchases = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 1, 1), TradeSide.Buy,
                    10, 1_000, 1_000, 0, 20, 0, 10_020, "Флоатер"),
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 2, 1), TradeSide.Buy,
                    5, 1_100, 1_000, 0, 10, 0, 5_510, "Флоатер")
            ],
            Security = new MoexBond
            {
                SecId = "BOND", BoardId = "TQCB", ShortName = "Флоатер", FaceValue = 1_000, FaceUnit = "RUB",
                BondType = "Облигация с плавающим купоном"
            },
            Marketdata = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 110 },
            AsOf = new DateOnly(2026, 2, 9),
            Coupons =
            [
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "RUB"),
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2), 60, "RUB")
            ]
        };
        yield return new TestCaseData(new Scenario(inputWithTwoPurchases,
                new ExpectedResult(15, 15_500d / 15, 1_100, 16_500, 1_000, 590, 1_560)))
            .SetName("Build_PurchasesInDifferentCouponPeriods");

        var inputWithPartialSale = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 1, 1), TradeSide.Buy,
                    10, 1_000, 1_000, 0, 20, 0, 10_020, "Флоатер"),
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 2, 1),
                    TradeSide.Sell, 4, 1_050, 1_000, 0, 0, 0, 4_200, "Флоатер")
            ],
            Security = new MoexBond
            {
                SecId = "BOND", BoardId = "TQCB", ShortName = "Флоатер", FaceValue = 1_000, FaceUnit = "RUB",
                BondType = "Облигация с плавающим купоном"
            },
            Marketdata = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 110 },
            AsOf = new DateOnly(2026, 2, 9),
            Coupons =
            [
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "RUB"),
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2), 60, "RUB")
            ]
        };
        yield return new TestCaseData(new Scenario(inputWithPartialSale,
                new ExpectedResult(6, 1_000, 1_100, 6_600, 600, 300, 888)))
            .SetName("Build_PartialSaleAfterOnePurchase");

        var inputWithPartialSaleAfterTwoPurchases = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 1, 1),
                    TradeSide.Buy, 10, 1_000, 1_000, 0, 20, 0, 10_020, "Флоатер"),
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 2, 1),
                    TradeSide.Buy, 5, 1_100, 1_000, 0, 10, 0, 5_510, "Флоатер"),
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 2, 5),
                    TradeSide.Sell, 6, 1_050, 1_000, 0, 0, 0, 6_300, "Флоатер")
            ],
            Security = new MoexBond
            {
                SecId = "BOND", BoardId = "TQCB", ShortName = "Флоатер", FaceValue = 1_000, FaceUnit = "RUB",
                BondType = "Облигация с плавающим купоном"
            },
            Marketdata = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 110 },
            AsOf = new DateOnly(2026, 2, 9),
            Coupons =
            [
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "RUB"),
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2), 60, "RUB")
            ]
        };
        yield return new TestCaseData(new Scenario(inputWithPartialSaleAfterTwoPurchases,
                new ExpectedResult(9, 15_500d / 15, 1_100, 9_900, 600, 354, 936)))
            .SetName("Build_PartialSaleAfterPurchasesAtDifferentPrices");

        var inputWithSaleOnCouponPeriodStart = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 1, 1),
                    TradeSide.Buy, 10, 1_000, 1_000, 0, 20, 0, 10_020, "Флоатер"),
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 2, 1),
                    TradeSide.Buy, 5, 1_100, 1_000, 0, 10, 0, 5_510, "Флоатер"),
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 3, 2),
                    TradeSide.Sell, 6, 1_050, 1_000, 0, 0, 0, 6_300, "Флоатер")
            ],
            Security = new MoexBond
            {
                SecId = "BOND", BoardId = "TQCB", ShortName = "Флоатер", FaceValue = 1_000, FaceUnit = "RUB",
                BondType = "Облигация с плавающим купоном"
            },
            Marketdata = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 110 },
            AsOf = new DateOnly(2026, 3, 11),
            Coupons =
            [
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "RUB"),
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2), 60, "RUB"),
                new OpenPositionCoupon("BOND", new DateOnly(2026, 3, 2), new DateOnly(2026, 4, 1), 90, "RUB")
            ]
        };
        yield return new TestCaseData(new Scenario(inputWithSaleOnCouponPeriodStart,
                new ExpectedResult(9, 15_500d / 15, 1_100, 9_900, 600, 984, 1_566)))
            .SetName("Build_PartialSaleOnFirstDayOfThirdCouponPeriod");

        var inputWithPurchaseAfterPartialSale = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 1, 1),
                    TradeSide.Buy, 10, 1_000, 1_000, 0, 20, 0, 10_020, "Флоатер"),
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 2, 1),
                    TradeSide.Sell, 4, 1_050, 1_000, 0, 0, 0, 4_200, "Флоатер"),
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 2, 3),
                    TradeSide.Buy, 5, 1_100, 1_000, 0, 10, 0, 5_510, "Флоатер")
            ],
            Security = new MoexBond
            {
                SecId = "BOND", BoardId = "TQCB", ShortName = "Флоатер", FaceValue = 1_000, FaceUnit = "RUB",
                BondType = "Облигация с плавающим купоном"
            },
            Marketdata = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 110 },
            AsOf = new DateOnly(2026, 2, 9),
            Coupons =
            [
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "RUB"),
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2), 60, "RUB")
            ]
        };
        yield return new TestCaseData(new Scenario(inputWithPurchaseAfterPartialSale,
                new ExpectedResult(11, 11_500d / 11, 1_100, 12_100, 600, 370, 948)))
            .SetName("Build_PurchaseAfterPartialSale");

        var inputWithMultiplePartialSales = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 1, 1),
                    TradeSide.Buy, 10, 1_000, 1_000, 0, 20, 0, 10_020, "Флоатер"),
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 2, 1),
                    TradeSide.Sell, 4, 1_050, 1_000, 0, 0, 0, 4_200, "Флоатер"),
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 2, 3),
                    TradeSide.Sell, 3, 1_050, 1_000, 0, 0, 0, 3_150, "Флоатер")
            ],
            Security = new MoexBond
            {
                SecId = "BOND", BoardId = "TQCB", ShortName = "Флоатер", FaceValue = 1_000, FaceUnit = "RUB",
                BondType = "Облигация с плавающим купоном"
            },
            Marketdata = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 110 },
            AsOf = new DateOnly(2026, 2, 9),
            Coupons =
            [
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "RUB"),
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2), 60, "RUB")
            ]
        };
        yield return new TestCaseData(new Scenario(inputWithMultiplePartialSales,
                new ExpectedResult(3, 1_000, 1_100, 3_300, 300, 150, 444)))
            .SetName("Build_MultiplePartialSales");

        var inputWithReopenedPosition = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 1, 1),
                    TradeSide.Buy, 10, 1_000, 1_000, 0, 20, 0, 10_020, "Флоатер"),
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 2, 1),
                    TradeSide.Sell, 10, 1_050, 1_000, 0, 0, 0, 10_500, "Флоатер"),
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 2, 3),
                    TradeSide.Buy, 5, 900, 1_000, 0, 10, 0, 4_510, "Флоатер")
            ],
            Security = new MoexBond
            {
                SecId = "BOND", BoardId = "TQCB", ShortName = "Флоатер", FaceValue = 1_000, FaceUnit = "RUB",
                BondType = "Облигация с плавающим купоном"
            },
            Marketdata = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 110 },
            AsOf = new DateOnly(2026, 2, 9),
            Coupons =
            [
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "RUB"),
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2), 60, "RUB")
            ]
        };
        yield return new TestCaseData(new Scenario(inputWithReopenedPosition,
                new ExpectedResult(5, 900, 1_100, 5_500, 1_000, 70, 1_060)))
            .SetName("Build_ReopenedPosition");

        var inputWithSameDayReopening = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.Parse("018cc251-f400-7000-8000-000000000003"), "BOND", "TQCB", "RUB",
                    new DateOnly(2026, 2, 1), TradeSide.Buy, 4, 900, 1_000, 0, 8, 0, 3_608, "Флоатер"),
                new PortfolioTradeRecord(Guid.Parse("018cc251-f400-7000-8000-000000000001"), "BOND", "TQCB", "RUB",
                    new DateOnly(2026, 1, 1), TradeSide.Buy, 10, 1_000, 1_000, 0, 20, 0, 10_020, "Флоатер"),
                new PortfolioTradeRecord(Guid.Parse("018cc251-f400-7000-8000-000000000002"), "BOND", "TQCB", "RUB",
                    new DateOnly(2026, 2, 1), TradeSide.Sell, 10, 1_050, 1_000, 0, 0, 0, 10_500, "Флоатер")
            ],
            Security = new MoexBond
            {
                SecId = "BOND", BoardId = "TQCB", ShortName = "Флоатер", FaceValue = 1_000, FaceUnit = "RUB",
                BondType = "Облигация с плавающим купоном"
            },
            Marketdata = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 110 },
            AsOf = new DateOnly(2026, 2, 9),
            Coupons =
            [
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "RUB"),
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2), 60, "RUB")
            ]
        };
        yield return new TestCaseData(new Scenario(inputWithSameDayReopening,
                new ExpectedResult(4, 900, 1_100, 4_400, 800, 72, 864)))
            .SetName("Build_ReopenedPositionOnSameDayInIdOrder");

        var inputWithMarketPriceBelowPurchase = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 1, 1),
                    TradeSide.Buy, 10, 1_000, 1_000, 0, 20, 0, 10_020, "Флоатер")
            ],
            Security = new MoexBond
            {
                SecId = "BOND", BoardId = "TQCB", ShortName = "Флоатер", FaceValue = 1_000, FaceUnit = "RUB",
                BondType = "Облигация с плавающим купоном"
            },
            Marketdata = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 90 },
            AsOf = new DateOnly(2026, 1, 10),
            Coupons =
            [
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "RUB")
            ]
        };
        yield return new TestCaseData(new Scenario(inputWithMarketPriceBelowPurchase,
                new ExpectedResult(10, 1_000, 900, 9_000, -1_000, 100, -920)))
            .SetName("Build_MarketPriceBelowPurchasePrice");

        var inputWithMarketPriceEqualToPurchase = new OpenPositionBuildInput
        {
            Trades =
            [
                new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 1, 1),
                    TradeSide.Buy, 10, 1_000, 1_000, 0, 20, 0, 10_020, "Флоатер")
            ],
            Security = new MoexBond
            {
                SecId = "BOND", BoardId = "TQCB", ShortName = "Флоатер", FaceValue = 1_000, FaceUnit = "RUB",
                BondType = "Облигация с плавающим купоном"
            },
            Marketdata = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 100 },
            AsOf = new DateOnly(2026, 1, 10),
            Coupons =
            [
                new OpenPositionCoupon("BOND", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "RUB")
            ]
        };
        yield return new TestCaseData(new Scenario(inputWithMarketPriceEqualToPurchase,
                new ExpectedResult(10, 1_000, 1_000, 10_000, 0, 100, 80)))
            .SetName("Build_MarketPriceEqualToPurchasePrice");
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
