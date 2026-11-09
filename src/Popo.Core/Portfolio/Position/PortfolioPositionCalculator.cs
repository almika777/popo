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

}
