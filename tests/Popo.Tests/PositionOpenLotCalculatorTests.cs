using NUnit.Framework;
using Popo.Core.Portfolio.Position;
using Popo.Core.Portfolio;

namespace Popo.Tests;

[TestFixture]
public sealed class PositionOpenLotCalculatorTests
{
    [Test]
    public void Calculate_SalesReduceEveryOpenPurchaseCohortProportionally()
    {
        var result = new PositionOpenLotCalculator().Calculate(
        [
            Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 10, 1_000, 5, 20),
            Trade(new DateOnly(2026, 2, 1), TradeSide.Buy, 5, 1_100, 8, 15),
            Trade(new DateOnly(2026, 7, 1), TradeSide.Sell, 12, 1_050)
        ], new DateOnly(2026, 12, 31));

        Assert.Multiple(() =>
        {
            Assert.That(result.RemainingQuantity, Is.EqualTo(3));
            Assert.That(result.RemainingCleanCost, Is.EqualTo(3_100));
            Assert.That(result.PaidBuyCommission, Is.EqualTo(7));
            Assert.That(result.PaidBuyAccruedInterest, Is.EqualTo(18).Within(0.000001));
            Assert.That(result.Lots, Has.Count.EqualTo(2));
            Assert.That(result.RemainingCleanCost / result.RemainingQuantity, Is.EqualTo(1_033.333333).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_SameDayBuyAndSellHaveNoExecutionOrder()
    {
        var date = new DateOnly(2026, 9, 1);
        var result = new PositionOpenLotCalculator().Calculate(
        [
            Trade(date, TradeSide.Sell, 1, 1_100),
            Trade(date, TradeSide.Buy, 1, 2_000),
            Trade(date, TradeSide.Buy, 1, 1_000)
        ], new DateOnly(2026, 12, 31));

        Assert.Multiple(() =>
        {
            Assert.That(result.RemainingQuantity, Is.EqualTo(1));
            Assert.That(result.RemainingCleanCost, Is.EqualTo(1_500));
            Assert.That(result.RemainingCleanCost / result.RemainingQuantity, Is.EqualTo(1_500));
            Assert.That(result.Lots, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Calculate_SameDaySaleDoesNotChangeWeightedAveragePurchasePrice()
    {
        var date = new DateOnly(2026, 9, 1);
        var result = new PositionOpenLotCalculator().Calculate(
        [
            Trade(date, TradeSide.Buy, 2, 1_000),
            Trade(date, TradeSide.Sell, 1, 1_100)
        ], new DateOnly(2026, 12, 31));

        Assert.Multiple(() =>
        {
            Assert.That(result.RemainingQuantity, Is.EqualTo(1));
            Assert.That(result.RemainingCleanCost / result.RemainingQuantity, Is.EqualTo(1_000));
        });
    }

    [Test]
    public void Calculate_RejectsSalesAboveTheAvailableQuantity()
    {
        Assert.Throws<InvalidOperationException>(() => new PositionOpenLotCalculator().Calculate(
        [
            Trade(new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 1_000),
            Trade(new DateOnly(2026, 2, 1), TradeSide.Sell, 2, 1_100)
        ], new DateOnly(2026, 12, 31)));
    }

    private static PositionIncomeTrade Trade(
        DateOnly date,
        TradeSide side,
        double quantity,
        double cleanPrice,
        double accruedInterest = 0,
        double commission = 0) =>
        new(date, side, quantity, cleanPrice, commission, null, cleanPrice, accruedInterest);
}
