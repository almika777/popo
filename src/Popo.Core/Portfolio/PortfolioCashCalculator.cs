namespace Popo.Core.Portfolio;

public static class PortfolioCashCalculator
{
    public static IReadOnlyList<CashBalance> Calculate(
        IReadOnlyCollection<CashSnapshot> snapshots,
        IReadOnlyCollection<PortfolioTrade> trades,
        DateOnly asOf,
        IReadOnlyCollection<MoneyMarketFundOperation>? fundOperations = null)
    {
        ValidateTrades(trades);

        var currencies = snapshots.Select(x => x.CurrencyId)
            .Concat(trades.Select(x => x.CurrencyId))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return currencies
            .Select(currency =>
            {
                var snapshot = snapshots
                    .Where(x => string.Equals(x.CurrencyId, currency, StringComparison.OrdinalIgnoreCase)
                                && x.SnapshotDate <= asOf)
                    .OrderByDescending(x => x.SnapshotDate)
                    .FirstOrDefault();
                var balance = snapshot?.Amount ?? 0;
                if (snapshot is not null)
                {
                    balance += trades
                        .Where(x => string.Equals(x.CurrencyId, currency, StringComparison.OrdinalIgnoreCase)
                                    && x.TradeDate > snapshot.SnapshotDate
                                    && x.TradeDate <= asOf)
                        .Sum(x => x.Side == TradeSide.Buy ? -x.Amount : x.Amount);

                    if (string.Equals(currency, "RUB", StringComparison.OrdinalIgnoreCase))
                    {
                        balance += MoneyMarketFundOperationCalculator.CashDelta(
                            (fundOperations ?? [])
                                .Where(x => x.Date > snapshot.SnapshotDate && x.Date <= asOf));
                    }
                }

                return new CashBalance(currency, balance);
            })
            .OrderBy(x => x.CurrencyId)
            .ToArray();
    }

    private static void ValidateTrades(IEnumerable<PortfolioTrade> trades)
    {
        foreach (var trade in trades)
        {
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
