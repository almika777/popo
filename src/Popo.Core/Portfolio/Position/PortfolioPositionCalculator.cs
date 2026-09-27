using Popo.Core.Portfolio.Trades;

namespace Popo.Core.Portfolio.Position;

public static class PortfolioPositionCalculator
{
    public static IReadOnlyList<PortfolioPosition> Calculate(IReadOnlyCollection<PortfolioTrade> trades)
    {
        PortfolioPositionsValidate.ValidateTrades(trades);

        var positions = trades
            .GroupBy(x => (x.SecId, x.BoardId, x.CurrencyId))
            .Select(group =>
            {
                var buys = group.Where(x => x.Side == TradeSide.Buy).ToArray();
                var sells = group.Where(x => x.Side == TradeSide.Sell).ToArray();

                return new PortfolioPosition(
                    group.Key.SecId,
                    group.Key.BoardId,
                    group.Key.CurrencyId,
                    buys.Sum(x => x.Quantity) - sells.Sum(x => x.Quantity),
                    buys.Sum(x => x.Quantity),
                    sells.Sum(x => x.Quantity),
                    buys.Sum(x => x.CleanAmount),
                    buys.Sum(x => x.Amount),
                    sells.Sum(x => x.Amount));
            })
            .OrderBy(x => x.SecId)
            .ThenBy(x => x.BoardId)
            .ThenBy(x => x.CurrencyId)
            .ToArray();

        return positions.Where(x => x.Quantity > 0).ToArray();
    }

    public static PortfolioPositionRecord ToRecord(PortfolioPosition position) =>
        new(
            position.SecId,
            position.BoardId,
            position.CurrencyId,
            position.Quantity,
            position.BuyQuantity,
            position.SellQuantity,
            position.BuyCleanAmount,
            position.BuyAmount,
            position.SellAmount,
            position.BuyQuantity == 0 ? 0 : position.BuyCleanAmount / position.BuyQuantity);

    public static PortfolioPositionIncome Calculate(
        IReadOnlyCollection<PositionIncomeTrade> trades,
        DateOnly asOf,
        double currentCleanPrice,
        double couponValue,
        int couponPeriodDays,
        bool includeIndexedNominalIncreaseInPnl = false)
    {
        ArgumentNullException.ThrowIfNull(trades);

        if (!double.IsFinite(currentCleanPrice) || currentCleanPrice <= 0
                                                || !double.IsFinite(couponValue) || couponValue < 0
                                                || couponPeriodDays < 0)
        {
            throw new ArgumentException("Текущая цена и данные по купону должны быть неотрицательными.");
        }

        var lots = new Queue<OpenLot>();
        foreach (var trade in trades.OrderBy(x => x.TradeDate).ThenBy(x => x.Side == TradeSide.Buy ? 0 : 1))
        {
            PortfolioPositionsValidate.ValidateTrade(trade, asOf);
            if (trade.Side == TradeSide.Buy)
            {
                lots.Enqueue(new OpenLot(
                    trade.TradeDate,
                    trade.Quantity,
                    trade.CleanPrice,
                    trade.ActualCleanPrice ?? trade.CleanPrice,
                    trade.CleanPricePercent,
                    trade.Commission / trade.Quantity));
                continue;
            }

            var quantityToSell = trade.Quantity;
            while (quantityToSell > 0 && lots.TryPeek(out var lot))
            {
                var consumedQuantity = Math.Min(quantityToSell, lot.Quantity);
                lot.Quantity -= consumedQuantity;
                quantityToSell -= consumedQuantity;
                if (lot.Quantity == 0)
                {
                    lots.Dequeue();
                }
            }

            if (quantityToSell > 0)
                throw new InvalidOperationException("Количество продажи не может превышать доступный остаток.");
        }

        var remainingLots = lots.ToArray();
        var remainingQuantity = remainingLots.Sum(x => x.Quantity);
        var remainingCleanCost = remainingLots.Sum(x => x.Quantity * x.CleanPrice);
        var actualRemainingCleanCost = remainingLots.Sum(x => x.Quantity * x.ActualCleanPrice);
        var paidBuyCommission = remainingLots.Sum(x => x.Quantity * x.CommissionPerUnit);
        double? averageBuyPricePercent = remainingQuantity == 0 || remainingLots.Any(x => !x.CleanPricePercent.HasValue)
            ? null
            : remainingLots.Sum(x => x.Quantity * x.CleanPricePercent!.Value) / remainingQuantity;
        var dailyCoupon = couponPeriodDays == 0 ? 0 : couponValue / couponPeriodDays;
        var couponIncome = remainingLots.Sum(x =>
            x.Quantity * dailyCoupon * Math.Max(0, asOf.DayNumber - x.TradeDate.DayNumber));
        var pnlCost = includeIndexedNominalIncreaseInPnl ? actualRemainingCleanCost : remainingCleanCost;
        var unrealizedPnl = remainingQuantity * currentCleanPrice - pnlCost;
        var unrealizedPnlPercent = pnlCost == 0 ? 0 : unrealizedPnl / pnlCost * 100;
        var totalPnl = unrealizedPnl
                       + couponIncome
                       - paidBuyCommission;
        var totalPnlPercent = pnlCost == 0 ? 0 : totalPnl / pnlCost * 100;

        return new PortfolioPositionIncome(
            remainingCleanCost,
            paidBuyCommission,
            couponIncome,
            totalPnl,
            totalPnlPercent,
            remainingQuantity,
            averageBuyPricePercent,
            unrealizedPnl,
            unrealizedPnlPercent,
            actualRemainingCleanCost);
    }
    public static PortfolioPositionValuation Calculate(
        PortfolioPositionRecord position,
        double? marketPrice)
        => Calculate(position, marketPrice, position.AverageBuyPrice);

    public static PortfolioPositionValuation Calculate(
        PortfolioPositionRecord position,
        double? marketPrice,
        double? averageBuyPriceAtCurrentFaceValue)
    {
        if (!marketPrice.HasValue || !double.IsFinite(marketPrice.Value) || marketPrice.Value <= 0)
        {
            return new PortfolioPositionValuation(null, null, null, null);
        }

        var marketValue = position.Quantity * marketPrice.Value;
        if (!averageBuyPriceAtCurrentFaceValue.HasValue
            || !double.IsFinite(averageBuyPriceAtCurrentFaceValue.Value)
            || averageBuyPriceAtCurrentFaceValue.Value <= 0)
        {
            return new PortfolioPositionValuation(marketPrice.Value, marketValue, null, null);
        }

        var cost = position.Quantity * averageBuyPriceAtCurrentFaceValue.Value;
        var unrealizedPnl = marketValue - cost;
        var unrealizedPnlPercent = cost == 0 ? 0 : unrealizedPnl / cost * 100;

        return new PortfolioPositionValuation(
            marketPrice.Value,
            marketValue,
            unrealizedPnl,
            unrealizedPnlPercent);
    }
}

sealed class OpenLot(
    DateOnly tradeDate,
    double quantity,
    double cleanPrice,
    double actualCleanPrice,
    double? cleanPricePercent,
    double commissionPerUnit)
{
    public DateOnly TradeDate { get; } = tradeDate;
    public double Quantity { get; set; } = quantity;
    public double CleanPrice { get; } = cleanPrice;
    public double ActualCleanPrice { get; } = actualCleanPrice;
    public double? CleanPricePercent { get; } = cleanPricePercent;
    public double CommissionPerUnit { get; } = commissionPerUnit;
}