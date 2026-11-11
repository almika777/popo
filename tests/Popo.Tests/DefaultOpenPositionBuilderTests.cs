using NUnit.Framework;
using Popo.Api.Services.Position;
using Popo.Api.Services.Position.Builders;
using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;

namespace Popo.Tests;

[TestFixture]
public sealed class DefaultOpenPositionBuilderTests
{
    [TestCaseSource(nameof(OpenPositionCases))]
    public void Build_CalculatesOpenPosition(Scenario scenario)
    {
        var builder = new DefaultOpenPositionBuilder();
        var marketdata = scenario.Marketdata ?? Marketdata();
        var security = scenario.Security ?? Bond();
        var result = builder.Build(new OpenPositionBuildInput
        {
            Trades = scenario.Trades,
            Marketdata = marketdata,
            Security = security,
            AsOf = scenario.AsOf
        });
        var expected = scenario.Expected;
        
        Assert.Multiple(() =>
        {
            Assert.That(result.ShortName, Is.EqualTo("Тестовая облигация"));
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
        var april = new DateOnly(2026, 4, 1);
        var january = new DateOnly(2026, 1, 10);
        yield return Case("WithoutSales",
            [Trade(new DateOnly(2026, 1, 10), TradeSide.Buy, 10, 1_000)],
            april, new ExpectedResult(10, 1_000, 1_100, 11_000, 1_000, 820, 1_820));
        yield return Case("PartialSale",
            [
                Trade(new DateOnly(2026, 1, 10), TradeSide.Buy, 10, 1_000),
                Trade(new DateOnly(2026, 2, 10), TradeSide.Buy, 5, 1_100),
                Trade(new DateOnly(2026, 3, 10), TradeSide.Sell, 12, 1_050)
            ],
            april, new ExpectedResult(3, 3_100d / 3, 1_100, 3_300, 200, 215, 415));
        yield return Case("ReopenedPosition",
            [
                Trade(new DateOnly(2026, 1, 10), TradeSide.Buy, 10, 1_000),
                Trade(new DateOnly(2026, 2, 10), TradeSide.Sell, 10, 1_050),
                Trade(new DateOnly(2026, 3, 10), TradeSide.Buy, 4, 900)
            ],
            april, new ExpectedResult(4, 900, 1_100, 4_400, 800, 92, 892));
        yield return Case("PartialSaleWithCouponAndCommission",
            [
                Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 10, 1_000) with { Commission = 20, Amount = 10_020 },
                Trade(new DateOnly(2026, 1, 6), TradeSide.Buy, 5, 1_100) with { Commission = 15, Amount = 5_515 },
                Trade(new DateOnly(2026, 1, 8), TradeSide.Sell, 12, 1_050)
            ],
            january, new ExpectedResult(3, 3_100d / 3, 1_100, 3_300, 200, 25, 218));
        yield return Case("ReopenedPositionExcludesPreviousCouponAndCommission",
            [
                Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 10, 1_000) with { Commission = 100 },
                Trade(new DateOnly(2026, 1, 5), TradeSide.Sell, 10, 1_050),
                Trade(new DateOnly(2026, 1, 9), TradeSide.Buy, 4, 900) with { Commission = 8 }
            ],
            january, new ExpectedResult(4, 900, 1_100, 4_400, 800, 8, 800));
        yield return Case("PurchaseAfterPartialSale",
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 10, 1_000),
             Trade(new DateOnly(2026, 1, 3), TradeSide.Sell, 5, 1_000),
             Trade(new DateOnly(2026, 1, 6), TradeSide.Buy, 5, 1_200)],
            january, new ExpectedResult(10, 1_100, 1_100, 11_000, 0, 75, 75));
        var otherBond = Bond();
        otherBond.FaceValue = 200;
        otherBond.CouponValue = 12;
        otherBond.CouponPeriod = 30;
        yield return new TestCaseData(new Scenario(
            [Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 2, 100)], january,
            new ExpectedResult(2, 100, 160, 320, 120, 8, 128),
            new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 80 }, otherBond))
            .SetName("Build_DifferentNominalPriceAndCoupon");
    }

    private static TestCaseData Case(
        string name,
        List<PortfolioTradeRecord> trades,
        DateOnly asOf,
        ExpectedResult expected) =>
        new TestCaseData(new Scenario(trades, asOf, expected))
            .SetName($"Build_{name}");

    public sealed record Scenario(List<PortfolioTradeRecord> Trades, DateOnly AsOf, ExpectedResult Expected,
        MoexMarketdata? Marketdata = null, MoexBond? Security = null);

    public sealed record ExpectedResult(
        int Quantity,
        double AvgBuyPrice,
        double MarketPrice,
        double TotalCost,
        double UnrealizedPnl,
        double CouponIncome,
        double TotalPnl);

    private static MoexBond Bond() => new()
    {
        SecId = "BOND", 
        BoardId = "TQCB", 
        ShortName = "Тестовая облигация",
        FaceValue = 1_000, 
        FaceUnit = "RUB",
        BondType = "Облигация с фиксированным (известным) купоном", 
        CouponValue = 90,
        CouponPeriod = 90
    };

    private static MoexMarketdata Marketdata() => new() { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 110 };

    private static PortfolioTradeRecord Trade(DateOnly date, TradeSide side, int quantity, double price) =>
        new(
            Id: Guid.NewGuid(),
            SecId: "BOND",
            BoardId: "TQCB",
            CurrencyId: "RUB",
            TradeDate: date,
            Side: side,
            Quantity: quantity,
            Price: price,
            FaceValue: 1_000,
            AccruedInterest: 0,
            Commission: 0,
            CommissionPercent: 0,
            Amount: quantity * price,
            ShortName: "Bond");
}
