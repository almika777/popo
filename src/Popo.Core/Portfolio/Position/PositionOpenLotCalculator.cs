namespace Popo.Core.Portfolio.Position;

public sealed class PositionOpenLotCalculator
{
    public PositionOpenLotsResult Calculate(
        IReadOnlyCollection<PositionIncomeTrade> trades,
        DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(trades);
        var lots = new List<OpenPositionLot>();

        foreach (var day in trades.GroupBy(x => x.TradeDate).OrderBy(x => x.Key))
        {
            var dayTrades = day.ToArray();
            foreach (var trade in dayTrades)
            {
                PortfolioPositionsValidate.ValidateTrade(trade, asOf);
                if (!double.IsFinite(trade.AccruedInterest) || trade.AccruedInterest < 0)
                    throw new ArgumentException("НКД сделки для расчёта результата позиции некорректен.");
            }

            foreach (var trade in dayTrades.Where(x => x.Side == TradeSide.Buy))
            {
                lots.Add(new OpenPositionLot(
                    trade.TradeDate,
                    trade.Quantity,
                    trade.CleanPrice,
                    trade.ActualCleanPrice ?? trade.CleanPrice,
                    trade.Commission / trade.Quantity,
                    trade.AccruedInterest,
                    trade.CleanPricePercent));
            }

            var quantityToSell = dayTrades.Where(x => x.Side == TradeSide.Sell).Sum(x => x.Quantity);
            var availableQuantity = lots.Sum(x => x.Quantity);
            if (quantityToSell > availableQuantity)
                throw new InvalidOperationException("Количество продажи не может превышать доступный остаток.");

            if (quantityToSell <= 0)
                continue;

            var retainedFraction = (availableQuantity - quantityToSell) / availableQuantity;
            if (retainedFraction <= 0)
            {
                lots.Clear();
                continue;
            }

            foreach (var lot in lots)
                lot.Quantity *= retainedFraction;
        }

        return new PositionOpenLotsResult(lots.Where(x => x.Quantity > 0).ToArray());
    }
}

public sealed class PositionOpenLotsResult(IReadOnlyList<OpenPositionLot> lots)
{
    public IReadOnlyList<OpenPositionLot> Lots { get; } = lots;
    public double RemainingQuantity => Lots.Sum(x => x.Quantity);
    public double RemainingCleanCost => Lots.Sum(x => x.Quantity * x.CleanPrice);
    public double ActualRemainingCleanCost => Lots.Sum(x => x.Quantity * x.ActualCleanPrice);
    public double PaidBuyCommission => Lots.Sum(x => x.Quantity * x.CommissionPerUnit);
    public double PaidBuyAccruedInterest => Lots.Sum(x => x.Quantity * x.PaidAccruedInterestPerUnit);
    public double? AverageBuyPricePercent => Lots.Count == 0 || Lots.Any(x => !x.CleanPricePercent.HasValue)
        ? null
        : Lots.Sum(x => x.Quantity * x.CleanPricePercent!.Value) / RemainingQuantity;
}
