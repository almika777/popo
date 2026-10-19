namespace Popo.Core.Portfolio;

public static class MoneyMarketFundOperationCalculator
{
    /// <summary>Применяет покупки и продажи к начальной позиции и пересчитывает остаток и среднюю себестоимость паёв.</summary>
    /// <param name="fund">Начальная позиция фонда.</param>
    /// <param name="operations">Операции фонда, упорядочиваемые по дате перед расчётом.</param>
    /// <returns>Количество паёв и их средняя себестоимость после всех операций.</returns>
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

    /// <summary>Рассчитывает сумму сделки по паям без учёта комиссии.</summary>
    /// <param name="operation">Операция покупки или продажи.</param>
    /// <returns>Количество паёв, умноженное на цену одного пая.</returns>
    public static double Amount(MoneyMarketFundOperation operation) =>
        operation.Quantity * operation.Price;

}

public sealed record MoneyMarketFundPosition(double Quantity, double AveragePrice);
