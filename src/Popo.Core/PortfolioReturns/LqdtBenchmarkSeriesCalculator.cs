using Popo.Core.Contracts.Iss;

namespace Popo.Core.PortfolioReturns;

internal sealed class LqdtBenchmarkSeriesCalculator
{
    private const string SecId = "LQDT";

    public LqdtBenchmarkSeriesCalculation Calculate(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<MoexIndexHistory> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (to < from)
        {
            throw new ArgumentException("Конечная дата не может быть раньше начальной.", nameof(to));
        }

        var prices = history
            .Where(value => string.Equals(value.SecId, SecId, StringComparison.Ordinal) &&
                            value.TradeDate.HasValue && value.TradeDate.Value >= from && value.TradeDate.Value <= to)
            .Select(ToPrice)
            .OrderBy(value => value.Date)
            .ToArray();

        if (prices.Length == 0)
        {
            throw new PortfolioBenchmarkHistoryUnavailableException(
                $"Нет торговых цен {SecId} в периоде с {from:yyyy-MM-dd} по {to:yyyy-MM-dd}.");
        }

        var duplicate = prices.GroupBy(value => value.Date).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidPortfolioBenchmarkHistoryException(
                $"В истории {SecId} несколько цен за {duplicate.Key:yyyy-MM-dd}.");
        }

        var startPrice = prices[0];
        var endPrice = prices[^1];
        var returnPercent = endPrice.Value / startPrice.Value - 1d;
        if (!double.IsFinite(returnPercent) || returnPercent < -1d)
        {
            throw new InvalidPortfolioBenchmarkHistoryException(
                $"Невозможно рассчитать доходность {SecId} по историческим ценам.");
        }

        var points = prices
            .Select(price => CreatePoint(price.Date, price.Value / startPrice.Value - 1d))
            .ToList();
        if (points[^1].Date < to)
        {
            points.Add(CreatePoint(to, returnPercent));
        }

        return new LqdtBenchmarkSeriesCalculation(
            startPrice.Date,
            endPrice.Date,
            new BenchmarkSeriesCalculation(returnPercent, returnPercent, points));
    }

    private static PriceValue ToPrice(MoexIndexHistory value)
    {
        if (value.TradeDate is null || value.Close is null || value.Close <= 0)
        {
            throw new InvalidPortfolioBenchmarkHistoryException("В истории MOEX найдено некорректное значение цены LQDT.");
        }

        return new PriceValue(value.TradeDate.Value, (double)value.Close.Value, true);
    }

    private static PortfolioBenchmarkPoint CreatePoint(DateOnly date, double returnPercent) =>
        new(date, returnPercent, returnPercent, returnPercent);
}
