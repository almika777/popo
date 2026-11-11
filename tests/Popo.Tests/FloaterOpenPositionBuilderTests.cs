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
        var builder = new FloaterOpenPositionBuilder();
        var result = builder.Build(new OpenPositionBuildInput
        {
            Trades = scenario.Trades,
            Marketdata = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 110 },
            Security = new MoexBond { SecId = "BOND", BoardId = "TQCB", ShortName = "Флоатер", FaceValue = 1_000, FaceUnit = "RUB" },
            AsOf = scenario.AsOf,
            Coupons = scenario.Coupons
        });
        var expected = scenario.Expected;

        Assert.Multiple(() =>
        {
            Assert.That(result.SecId, Is.EqualTo("BOND"));
            Assert.That(result.BoardId, Is.EqualTo("TQCB"));
            Assert.That(result.CurrencyId, Is.EqualTo("RUB"));
            Assert.That(result.ShortName, Is.EqualTo("Флоатер"));
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
        var start = new DateOnly(2026, 1, 1);
        var second = start.AddDays(30);
        var end = second.AddDays(30);
        var thirdEnd = end.AddDays(30);
        OpenPositionCoupon[] coupons = [new("BOND", start, second, 30, "RUB"),
            new("BOND", second, end, 60, "RUB"), new("BOND", end, thirdEnd, 90, "RUB")];
        yield return Case("WithoutSales", [Trade(start, TradeSide.Buy, 10, 1_000)], start.AddDays(9), coupons,
            new(10, 1_000, 1_100, 11_000, 1_000, 100, 1_100));
        yield return Case("NinetyDaysAcrossThreeCouponPeriods",
            [Trade(start, TradeSide.Buy, 2, 1_000)], thirdEnd.AddDays(-1), coupons,
            new(2, 1_000, 1_100, 2_200, 200, 360, 560));
        yield return Case("AssessmentOnFirstDayOfNextCouponPeriod",
            [Trade(start, TradeSide.Buy, 2, 1_000)], second, coupons,
            new(2, 1_000, 1_100, 2_200, 200, 64, 264));
        yield return Case("PartialSaleWithDifferentCoupons",
            [Trade(start, TradeSide.Buy, 10, 1_000, 20), Trade(second, TradeSide.Buy, 10, 1_100, 40),
             Trade(end.AddDays(-1), TradeSide.Sell, 10, 1_050)], end.AddDays(-1), coupons,
            new(10, 1_050, 1_100, 11_000, 500, 750, 1_220));
        yield return Case("PurchaseAfterPartialSale",
            [Trade(start, TradeSide.Buy, 10, 1_000), Trade(start.AddDays(9), TradeSide.Sell, 5, 1_000),
             Trade(second, TradeSide.Buy, 5, 1_200)], second.AddDays(9), coupons,
            new(10, 1_100, 1_100, 11_000, 0, 350, 350));
        yield return Case("ReopenedPosition",
            [Trade(start, TradeSide.Buy, 10, 1_000, 20), Trade(start.AddDays(19), TradeSide.Sell, 10, 1_050),
             Trade(start.AddDays(20), TradeSide.Buy, 4, 900, 8)], start.AddDays(24), coupons,
            new(4, 900, 1_100, 4_400, 800, 20, 812));
        OpenPositionCoupon[] unknownCurrent = [new("BOND", start, second, 30, "RUB"), new("BOND", second, end, null, "RUB")];
        yield return Case("UsesPreviousCouponWhenCurrentUnknown",
            [Trade(second, TradeSide.Buy, 5, 1_000)], second.AddDays(9), unknownCurrent,
            new(5, 1_000, 1_100, 5_500, 500, 50, 550));
        OpenPositionCoupon[] unknownPast = [new("BOND", start, second, 30, "RUB"),
            new("BOND", second, end, null, "RUB"), new("BOND", end, thirdEnd, 90, "RUB")];
        yield return Case("UsesEarlierCouponForUnknownPastPeriod",
            [Trade(second, TradeSide.Buy, 2, 1_000)], end.AddDays(9), unknownPast,
            new(2, 1_000, 1_100, 2_200, 200, 120, 320));
    }

    private static TestCaseData Case(string name, List<PortfolioTradeRecord> trades, DateOnly asOf,
        IReadOnlyList<OpenPositionCoupon> coupons, ExpectedResult expected)
        => new TestCaseData(new Scenario(trades, asOf, coupons, expected)).SetName($"Build_{name}");

    public sealed record Scenario(List<PortfolioTradeRecord> Trades, DateOnly AsOf,
        IReadOnlyList<OpenPositionCoupon> Coupons, ExpectedResult Expected);

    public sealed record ExpectedResult(int Quantity, double AvgBuyPrice, double MarketPrice, double TotalCost,
        double UnrealizedPnl, double CouponIncome, double TotalPnl);

    private static PortfolioTradeRecord Trade(DateOnly date, TradeSide side, int quantity, double price, double commission = 0)
        => new(Guid.NewGuid(), "BOND", "TQCB", "RUB", date, side, quantity, price, 1_000, 0, commission, 0,
            quantity * price + commission, "Флоатер");
}
