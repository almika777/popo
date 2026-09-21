namespace Popo.Core.Portfolio;

public sealed record PositionIncomeTrade(
    DateOnly TradeDate,
    TradeSide Side,
    double Quantity,
    double CleanPrice,
    double Commission);

public sealed record PortfolioPositionIncome(
    double RemainingCleanCost,
    double PaidBuyCommission,
    double CouponIncome,
    double TotalPnl,
    double TotalPnlPercent);

public static class PortfolioPositionIncomeCalculator
{
    public static PortfolioPositionIncome Calculate(
        IReadOnlyCollection<PositionIncomeTrade> trades,
        DateOnly asOf,
        double currentCleanPrice,
        double couponValue,
        int couponPeriodDays)
    {
        ArgumentNullException.ThrowIfNull(trades);

        if (!double.IsFinite(currentCleanPrice) || currentCleanPrice <= 0
            || !double.IsFinite(couponValue) || couponValue < 0
            || couponPeriodDays < 0)
        {
                throw new ArgumentException("Текущая цена и данные по купону должны быть неотрицательными.");
        }

        var lots = new Queue<OpenLot>();
        foreach (var trade in trades
                     .OrderBy(x => x.TradeDate)
                     .ThenBy(x => x.Side == TradeSide.Buy ? 0 : 1))
        {
            ValidateTrade(trade, asOf);
            if (trade.Side == TradeSide.Buy)
            {
                lots.Enqueue(new OpenLot(
                    trade.TradeDate,
                    trade.Quantity,
                    trade.CleanPrice,
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
            {
                throw new InvalidOperationException("Количество продажи не может превышать доступный остаток.");
            }
        }

        var remainingLots = lots.ToArray();
        var remainingQuantity = remainingLots.Sum(x => x.Quantity);
        var remainingCleanCost = remainingLots.Sum(x => x.Quantity * x.CleanPrice);
        var paidBuyCommission = remainingLots.Sum(x => x.Quantity * x.CommissionPerUnit);
        var dailyCoupon = couponPeriodDays == 0 ? 0 : couponValue / couponPeriodDays;
        var couponIncome = remainingLots.Sum(x =>
            x.Quantity * dailyCoupon * Math.Max(0, asOf.DayNumber - x.TradeDate.DayNumber));
        var totalPnl = remainingQuantity * currentCleanPrice
                       - remainingCleanCost
                       + couponIncome
                       - paidBuyCommission;
        var totalPnlPercent = remainingCleanCost == 0 ? 0 : totalPnl / remainingCleanCost * 100;

        return new PortfolioPositionIncome(
            remainingCleanCost,
            paidBuyCommission,
            couponIncome,
            totalPnl,
            totalPnlPercent);
    }

    private static void ValidateTrade(PositionIncomeTrade trade, DateOnly asOf)
    {
        if (!Enum.IsDefined(trade.Side)
            || trade.TradeDate > asOf
            || !double.IsFinite(trade.Quantity) || trade.Quantity <= 0
            || !double.IsFinite(trade.CleanPrice) || trade.CleanPrice <= 0
            || !double.IsFinite(trade.Commission) || trade.Commission < 0)
        {
                throw new ArgumentException("Данные сделки для расчёта результата позиции некорректны.");
        }
    }

    private sealed class OpenLot(
        DateOnly tradeDate,
        double quantity,
        double cleanPrice,
        double commissionPerUnit)
    {
        public DateOnly TradeDate { get; } = tradeDate;
        public double Quantity { get; set; } = quantity;
        public double CleanPrice { get; } = cleanPrice;
        public double CommissionPerUnit { get; } = commissionPerUnit;
    }
}
