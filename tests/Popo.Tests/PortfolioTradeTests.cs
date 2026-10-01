using NUnit.Framework;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;

namespace Popo.Tests;

public sealed class PortfolioTradeTests
{
    [Test]
    public void PortfolioTrade_DefaultsFaceValueToOneThousand()
    {
        var trade = new PortfolioTrade(
            "RU000A", "TQCB", "RUB", new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 100);

        Assert.That(trade.FaceValue, Is.EqualTo(1_000));
    }

    [Test]
    public void Buy_UsesPriceAndAccruedInterestAndAddsPercentCommission()
    {
        var result = TradeSettlementCalculator.Calculate(TradeSide.Buy, 489, 1_026.24, 4.62, 0.04);

        Assert.Multiple(() =>
        {
            Assert.That(result.GrossAmount, Is.EqualTo(504_090.54).Within(0.000001));
            Assert.That(result.CommissionAmount, Is.EqualTo(201.636216).Within(0.000001));
            Assert.That(result.NetAmount, Is.EqualTo(504_292.176216).Within(0.000001));
        });
    }

    [Test]
    public void Sell_UsesPriceAndAccruedInterestAndSubtractsPercentCommission()
    {
        var result = TradeSettlementCalculator.Calculate(TradeSide.Sell, 489, 1_026.24, 4.62, 0.04);

        Assert.That(result.NetAmount, Is.EqualTo(503_888.903784).Within(0.000001));
    }

    [Test]
    public void SellingMoreThanHeld_IsRejected()
    {
        var tradeDate = new DateOnly(2026, 1, 1);

        Assert.Throws<InvalidOperationException>(() => PortfolioPositionCalculator.Calculate(
            [new PositionIncomeTrade(tradeDate, TradeSide.Sell, 1, 100, 0)],
            tradeDate,
            currentCleanPrice: 100,
            couponValue: 0,
            couponPeriodDays: 0));
    }

    [Test]
    public void Calculate_RejectsSaleBeforeLaterPurchaseEvenWhenFinalQuantityIsPositive()
    {
        var secId = "RU000A";
        const string boardId = "TQCB";
        const string currencyId = "RUB";

        Assert.Throws<InvalidOperationException>(() => PortfolioPositionCalculator.Calculate([
            new PortfolioTrade(secId, boardId, currencyId, new DateOnly(2026, 1, 1), TradeSide.Sell, 5, 99),
            new PortfolioTrade(secId, boardId, currencyId, new DateOnly(2026, 1, 2), TradeSide.Buy, 10, 98)
        ]));
    }

    [TestCase(TradeSide.Buy)]
    [TestCase(TradeSide.Sell)]
    public void ZeroAccruedInterestAndCommission_UseCleanPriceOnly(TradeSide side)
    {
        var result = TradeSettlementCalculator.Calculate(side, 10, 950, 0, 0);

        Assert.That(result.GrossAmount, Is.EqualTo(9_500));
        Assert.That(result.CommissionAmount, Is.Zero);
        Assert.That(result.NetAmount, Is.EqualTo(9_500));
    }

    [Test]
    public void ManualAccruedInterest_IsUsedInsteadOfAnyMarketValue()
    {
        var result = TradeSettlementCalculator.Calculate(TradeSide.Buy, 100, 1_000, 35.7666, 0);

        Assert.That(result.GrossAmount, Is.EqualTo(103_576.66).Within(0.000001));
    }

    [TestCase(0, 100, 0, 0)]
    [TestCase(-1, 100, 0, 0)]
    [TestCase(1.5, 100, 0, 0)]
    [TestCase(1, 0, 0, 0)]
    [TestCase(1, -1, 0, 0)]
    [TestCase(1, 100, -0.01, 0)]
    [TestCase(1, 100, 0, -0.01)]
    public void InvalidTradeValues_AreRejected(double quantity, double price, double accruedInterest, double commissionPercent)
    {
        Assert.Throws<ArgumentException>(() => TradeSettlementCalculator.Calculate(
            TradeSide.Buy, quantity, price, accruedInterest, commissionPercent));
    }

    [Test]
    public void FractionalQuantity_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => PortfolioPositionCalculator.Calculate([
            new PortfolioTrade("RU000A", "TQCB", "RUB", new DateOnly(2026, 1, 1), TradeSide.Buy, 1.5, 95, 1_000, 0, 0)
        ]));
    }

    [Test]
    public void Trade_AmountIncludesFaceValueAccruedInterestAndCommission()
    {
        var buy = new PortfolioTrade("RU000A", "TQCB", "RUB", new DateOnly(2026, 1, 1), TradeSide.Buy, 10, 950, 1_000, 12, 5);
        var sell = new PortfolioTrade("RU000A", "TQCB", "RUB", new DateOnly(2026, 1, 2), TradeSide.Sell, 10, 950, 1_000, 12, 5);

        Assert.That(buy.GrossAmount, Is.EqualTo(9_620).Within(0.001));
        var buyWithPercentCommission = buy with { CommissionPercent = 0.04 };
        var sellWithPercentCommission = sell with { CommissionPercent = 0.04 };

        Assert.That(buyWithPercentCommission.CommissionAmount, Is.EqualTo(3.848).Within(0.001));
        Assert.That(buyWithPercentCommission.Amount, Is.EqualTo(9_623.848).Within(0.001));
        Assert.That(sellWithPercentCommission.Amount, Is.EqualTo(9_616.152).Within(0.001));
    }
}
