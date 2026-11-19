namespace Popo.Core.Portfolio.Position;

public class PortfolioPositionsValidate
{
    public static void ValidateTrades(IEnumerable<PortfolioTrade> trades)
    {
        foreach (var trade in trades)
        {
            if (string.IsNullOrWhiteSpace(trade.SecId)
                || string.IsNullOrWhiteSpace(trade.BoardId)
                || string.IsNullOrWhiteSpace(trade.CurrencyId))
            {
                throw new ArgumentException("Укажите бумагу, торговую площадку и валюту сделки.");
            }

            if (trade.Quantity <= 0
                || !double.IsFinite(trade.Price) || trade.Price <= 0
                || !double.IsFinite(trade.FaceValue) || trade.FaceValue <= 0
                || !double.IsFinite(trade.AccruedInterest) || trade.AccruedInterest < 0
                || !double.IsFinite(trade.CommissionPercent) || trade.CommissionPercent < 0)
            {
                throw new ArgumentException("Количество и цена сделки должны быть положительными.");
            }
        }
    }

    public static void ValidateTradeSequence(IEnumerable<PortfolioTrade> trades)
    {
        ArgumentNullException.ThrowIfNull(trades);

        var tradeList = trades.ToArray();
        ValidateTrades(tradeList);

        foreach (var positionTrades in tradeList.GroupBy(x => (x.SecId, x.BoardId, x.CurrencyId)))
        {
            var availableQuantity = 0d;
            foreach (var trade in positionTrades.OrderBy(x => x.TradeDate)
                         .ThenBy(x => x.Id.HasValue ? 0 : 1)
                         .ThenBy(x => x.Id)
                         .ThenBy(x => x.Side))
            {
                availableQuantity += trade.Side == TradeSide.Buy ? trade.Quantity : -trade.Quantity;
                if (availableQuantity < 0)
                {
                    throw new InvalidOperationException("Количество продажи не может превышать доступный остаток на дату сделки.");
                }
            }
        }
    }

}
