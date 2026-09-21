namespace Popo.Core.Portfolio;

public static class MoneyMarketFundOperationCalculator
{
    public static MoneyMarketFundPosition CalculatePosition(
        MoneyMarketFund fund,
        IEnumerable<MoneyMarketFundOperation> operations)
    {
        var quantity = fund.Quantity;
        var cost = fund.Quantity * fund.AveragePrice;

        foreach (var operation in operations.OrderBy(x => x.Date))
        {
            if (operation.Side == TradeSide.Buy)
            {
                quantity += operation.Quantity;
                cost += Amount(operation) + operation.Commission;
                continue;
            }

            if (operation.Quantity > quantity)
            {
                throw new InvalidOperationException("Нельзя продать больше паёв фонда, чем есть в наличии.");
            }

            var averagePrice = quantity == 0 ? 0 : cost / quantity;
            quantity -= operation.Quantity;
            cost -= operation.Quantity * averagePrice;
        }

        return new MoneyMarketFundPosition(
            quantity,
            quantity == 0 ? 0 : cost / quantity);
    }

    public static double Amount(MoneyMarketFundOperation operation) =>
        operation.Quantity * operation.Price;

    public static double CashDelta(IEnumerable<MoneyMarketFundOperation> operations) =>
        operations.Sum(operation => operation.Side == TradeSide.Buy
            ? -Amount(operation) - operation.Commission
            : Amount(operation) - operation.Commission);

    public static double QuantityDelta(IEnumerable<MoneyMarketFundOperation> operations) =>
        operations.Sum(operation => operation.Side == TradeSide.Buy ? operation.Quantity : -operation.Quantity);
}

public sealed record MoneyMarketFundPosition(double Quantity, double AveragePrice);
