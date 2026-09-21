namespace Popo.Core.Portfolio;

public static class PortfolioPositionCalculator
{
    public static IReadOnlyList<PortfolioPosition> Calculate(
        IReadOnlyCollection<PortfolioTrade> trades)
    {
        ValidateTrades(trades);

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

        if (positions.Any(x => x.Quantity < 0))
        {
            throw new InvalidOperationException("Количество позиции не может быть отрицательным.");
        }

        return positions.Where(x => x.Quantity > 0).ToArray();
    }

    public static PortfolioPositionRecord ToRecord(PortfolioPosition position) =>
        new(
            position.SecId,
            position.BoardId,
            position.CurrencyId,
            position.Quantity,
            position.BoughtQuantity,
            position.SoldQuantity,
            position.BoughtCleanAmount,
            position.BoughtAmount,
            position.SoldAmount,
            position.BoughtQuantity == 0 ? 0 : position.BoughtCleanAmount / position.BoughtQuantity);

    private static void ValidateTrades(IEnumerable<PortfolioTrade> trades)
    {
        foreach (var trade in trades)
        {
            if (string.IsNullOrWhiteSpace(trade.SecId)
                || string.IsNullOrWhiteSpace(trade.BoardId)
                || string.IsNullOrWhiteSpace(trade.CurrencyId))
            {
                throw new ArgumentException("Укажите бумагу, торговую площадку и валюту сделки.");
            }

            if (!double.IsFinite(trade.Quantity) || trade.Quantity <= 0 || trade.Quantity != Math.Truncate(trade.Quantity)
                || !double.IsFinite(trade.Price) || trade.Price <= 0
                || !double.IsFinite(trade.FaceValue) || trade.FaceValue <= 0
                || !double.IsFinite(trade.AccruedInterest) || trade.AccruedInterest < 0
                || !double.IsFinite(trade.CommissionPercent) || trade.CommissionPercent < 0)
            {
                throw new ArgumentException("Количество и цена сделки должны быть положительными.");
            }
        }
    }
}
