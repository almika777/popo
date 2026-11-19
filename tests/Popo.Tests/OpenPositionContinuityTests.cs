using NUnit.Framework;
using Popo.Api.Services.Position;
using Popo.Api.Services.Position.Builders;
using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio.Position;
using Popo.Core.Portfolio;

namespace Popo.Tests;

[TestFixture]
public sealed class OpenPositionContinuityTests
{
    [Test]
    public void Calculate_SalesReduceEveryOpenPurchaseCohortProportionally()
    {
        var result = Build(
        [
            Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 10, 1_000, 5, 20),
            Trade(new DateOnly(2026, 2, 1), TradeSide.Buy, 5, 1_100, 8, 15),
            Trade(new DateOnly(2026, 7, 1), TradeSide.Sell, 12, 1_050)
        ], new DateOnly(2026, 7, 1));

        Assert.Multiple(() =>
        {
            Assert.That(result.Quantity, Is.EqualTo(3));
            Assert.That(result.AvgBuyPrice, Is.EqualTo(3_100d / 3).Within(0.000001));
            Assert.That(result.UnrealizedPnl, Is.EqualTo(200).Within(0.000001));
            Assert.That(result.CouponIncome, Is.EqualTo(515).Within(0.000001));
            Assert.That(result.TotalPnl, Is.EqualTo(708).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_SameDayPurchasesBeforeSaleUseIdOrder()
    {
        var date = new DateOnly(2026, 9, 1);
        var result = Build(
        [
            Trade(date, TradeSide.Sell, 1, 1_100) with { Id = Guid.Parse("018cc251-f400-7000-8000-000000000003") },
            Trade(date, TradeSide.Buy, 1, 2_000) with { Id = Guid.Parse("018cc251-f400-7000-8000-000000000001") },
            Trade(date, TradeSide.Buy, 1, 1_000) with { Id = Guid.Parse("018cc251-f400-7000-8000-000000000002") }
        ], new DateOnly(2026, 12, 31));

        Assert.Multiple(() =>
        {
            Assert.That(result.Quantity, Is.EqualTo(1));
            Assert.That(result.AvgBuyPrice, Is.EqualTo(1_500));
        });
    }

    [Test]
    public void Calculate_SameDaySaleDoesNotChangeWeightedAveragePurchasePrice()
    {
        var date = new DateOnly(2026, 9, 1);
        var result = Build(
        [
            Trade(date, TradeSide.Buy, 2, 1_000) with { Id = Guid.Parse("018cc251-f400-7000-8000-000000000001") },
            Trade(date, TradeSide.Sell, 1, 1_100) with { Id = Guid.Parse("018cc251-f400-7000-8000-000000000002") }
        ], new DateOnly(2026, 12, 31));

        Assert.Multiple(() =>
        {
            Assert.That(result.Quantity, Is.EqualTo(1));
            Assert.That(result.AvgBuyPrice, Is.EqualTo(1_000));
        });
    }

    [Test]
    public void Calculate_RejectsSalesAboveTheAvailableQuantity()
    {
        Assert.Throws<InvalidOperationException>(() => PortfolioPositionsValidate.ValidateTradeSequence(
        [
            new PortfolioTrade("BOND", "TQCB", "RUB", new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000),
            new PortfolioTrade("BOND", "TQCB", "RUB", new DateOnly(2026, 2, 1), TradeSide.Sell, 2, 1_100)
        ]));
    }

    private static Popo.Api.Models.PositionResponse Build(IReadOnlyList<PortfolioTradeRecord> trades, DateOnly asOf) =>
        new DefaultOpenPositionBuilder().Build(new OpenPositionBuildInput
        {
            Trades = trades,
            AsOf = asOf,
            Security = new MoexBond { SecId = "BOND", BoardId = "TQCB", ShortName = "Bond", FaceValue = 1_000,
                FaceUnit = "RUB", BondType = "Фикс с известным купоном", CouponValue = 90, CouponPeriod = 90 },
            Marketdata = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 110 }
        });

    private static PortfolioTradeRecord Trade(
        DateOnly date,
        TradeSide side,
        int quantity,
        double cleanPrice,
        double accruedInterest = 0,
        double commission = 0) =>
        new(Guid.NewGuid(), "BOND", "TQCB", "RUB", date, side, quantity, cleanPrice, 1_000,
            accruedInterest, commission, 0, quantity * (cleanPrice + accruedInterest) + commission);
}
