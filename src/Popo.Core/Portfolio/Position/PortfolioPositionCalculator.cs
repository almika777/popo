namespace Popo.Core.Portfolio.Position;

public static class PortfolioPositionCalculator
{
    public static IReadOnlyList<PortfolioPosition> Calculate(IReadOnlyCollection<PortfolioTrade> trades)
    {
        PortfolioPositionsValidate.ValidateTradeSequence(trades);

        var positions = trades
            .GroupBy(x => (x.SecId, x.BoardId, x.CurrencyId))
            .Select(group =>
            {
                var buys = group.Where(x => x.Side == TradeSide.Buy).ToArray();
                var sells = group.Where(x => x.Side == TradeSide.Sell).ToArray();

                var position = new PortfolioPosition(
                    group.Key.SecId,
                    group.Key.BoardId,
                    group.Key.CurrencyId,
                    buys.Sum(x => x.Quantity) - sells.Sum(x => x.Quantity),
                    buys.Sum(x => x.Quantity),
                    sells.Sum(x => x.Quantity),
                    buys.Sum(x => x.CleanAmount),
                    buys.Sum(x => x.Amount),
                    sells.Sum(x => x.Amount));
                
                var openLots = PositionOpenLotCalculator.Calculate(
                    group.Select(x => new PositionIncomeTrade(
                        x.TradeDate,
                        x.Side,
                        x.Quantity,
                        x.Price,
                        x.CommissionAmount,
                        ActualCleanPrice: x.Price,
                        AccruedInterest: x.AccruedInterest)).ToArray(),
                    DateOnly.MaxValue);

                return position with
                {
                    AverageBuyPrice = openLots.RemainingQuantity > 0
                        ? openLots.RemainingCleanCost / openLots.RemainingQuantity
                        : null
                };
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
            position.AverageBuyPrice);

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

        var openLots = PositionOpenLotCalculator.Calculate(trades, asOf);
        var dailyCoupon = couponPeriodDays == 0 ? 0 : couponValue / couponPeriodDays;
        var couponIncome = openLots.Lots
            .Sum(x => x.Quantity * dailyCoupon * Math.Max(0, asOf.DayNumber - x.TradeDate.DayNumber));
        var pnlCost = includeIndexedNominalIncreaseInPnl ? openLots.ActualRemainingCleanCost : openLots.RemainingCleanCost;
        var unrealizedPnl = openLots.RemainingQuantity * currentCleanPrice - pnlCost;
        var unrealizedPnlPercent = pnlCost == 0 ? 0 : unrealizedPnl / pnlCost * 100;
        var totalPnl = unrealizedPnl
                       + couponIncome
                       - openLots.PaidBuyCommission;
        var totalPnlPercent = pnlCost == 0 ? 0 : totalPnl / pnlCost * 100;
        var annualizedPnlAmount = openLots.Lots.Sum(lot =>
        {
            var lotCostPerUnit = includeIndexedNominalIncreaseInPnl ? lot.ActualCleanPrice : lot.CleanPrice;
            var holdingDays = Math.Max(1, asOf.DayNumber - lot.TradeDate.DayNumber);
            var lotPnl = lot.Quantity * (currentCleanPrice - lotCostPerUnit)
                         + lot.Quantity * dailyCoupon * holdingDays
                         - lot.Quantity * lot.CommissionPerUnit;
            return lotPnl * 365 / holdingDays;
        });
        double? annualizedTotalPnlPercent = pnlCost <= 0
            ? null
            : annualizedPnlAmount / pnlCost * 100;

        return new PortfolioPositionIncome(
            openLots.RemainingCleanCost,
            openLots.PaidBuyCommission,
            couponIncome,
            totalPnl,
            totalPnlPercent,
            openLots.RemainingQuantity,
            openLots.AverageBuyPricePercent,
            unrealizedPnl,
            unrealizedPnlPercent,
            openLots.ActualRemainingCleanCost,
            annualizedTotalPnlPercent);
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
