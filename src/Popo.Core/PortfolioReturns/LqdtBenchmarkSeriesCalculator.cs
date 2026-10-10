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

        var rows = history
            .Where(value => string.Equals(value.SecId, SecId, StringComparison.Ordinal) && value.TradeDate.HasValue)
            .OrderBy(value => value.TradeDate)
            .ToArray();
        var periodRows = rows
            .Where(value => value.TradeDate!.Value >= from && value.TradeDate.Value <= to)
            .ToArray();
        if (periodRows.Length == 0)
        {
            throw new PortfolioBenchmarkHistoryUnavailableException(
                $"Нет торговых цен {SecId} в периоде с {from:yyyy-MM-dd} по {to:yyyy-MM-dd}.");
        }

        var hasQuoteOnEndDate = periodRows.Any(value => value.TradeDate!.Value == to);
        var nextQuoteDate = hasQuoteOnEndDate
            ? null
            : rows.FirstOrDefault(value => value.TradeDate!.Value > to)?.TradeDate;
        var nextQuoteRows = nextQuoteDate.HasValue
            ? rows.Where(value => value.TradeDate == nextQuoteDate).ToArray()
            : [];
        var previousQuoteRows = periodRows.Length == 1 && periodRows[0].TradeDate!.Value < to && nextQuoteRows.Length == 0
            ? GetPreviousQuoteRows(rows, periodRows[0].TradeDate!.Value)
            : [];
        var relevantRows = periodRows.Concat(nextQuoteRows).Concat(previousQuoteRows).ToArray();

        var duplicate = relevantRows
            .GroupBy(value => value.TradeDate)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidPortfolioBenchmarkHistoryException(
                $"В истории {SecId} несколько цен за {duplicate.Key:yyyy-MM-dd}.");
        }

        var periodPrices = periodRows.Select(ToPrice).ToArray();
        var startPrice = periodPrices[0];
        var endPrice = periodPrices[^1];
        var actualPrices = periodPrices
            .Concat(nextQuoteRows.Select(ToPrice))
            .OrderBy(value => value.Date)
            .ToArray();
        var priorPrice = previousQuoteRows.Select(ToPrice).SingleOrDefault();
        PriceValue[] extrapolationPrices = priorPrice is null
            ? actualPrices
            : [priorPrice, .. actualPrices];

        var points = new List<PortfolioBenchmarkPoint>();
        var date = startPrice.Date;
        while (true)
        {
            var price = GetPriceAtDate(date, actualPrices, extrapolationPrices);
            points.Add(CreatePoint(date, price / startPrice.Value - 1d));
            if (date == to)
            {
                break;
            }

            date = date.AddDays(1);
        }

        var returnPercent = points[^1].ProfitPercent!.Value;
        if (!double.IsFinite(returnPercent) || returnPercent < -1d)
        {
            throw new InvalidPortfolioBenchmarkHistoryException(
                $"Невозможно рассчитать доходность {SecId} по историческим ценам.");
        }

        return new LqdtBenchmarkSeriesCalculation(
            startPrice.Date,
            endPrice.Date,
            new BenchmarkSeriesCalculation(returnPercent, points));
    }

    private static IReadOnlyCollection<MoexIndexHistory> GetPreviousQuoteRows(
        IReadOnlyCollection<MoexIndexHistory> rows,
        DateOnly date)
    {
        var previousRow = rows.LastOrDefault(value => value.TradeDate!.Value < date);
        return previousRow is null
            ? []
            : rows.Where(value => value.TradeDate == previousRow.TradeDate).ToArray();
    }

    private static double GetPriceAtDate(
        DateOnly date,
        IReadOnlyList<PriceValue> actualPrices,
        IReadOnlyList<PriceValue> extrapolationPrices)
    {
        var nextIndex = -1;
        for (var index = 0; index < actualPrices.Count; index++)
        {
            if (actualPrices[index].Date >= date)
            {
                nextIndex = index;
                break;
            }
        }

        if (nextIndex >= 0)
        {
            var next = actualPrices[nextIndex];
            if (next.Date == date || nextIndex == 0)
            {
                return next.Value;
            }

            return InterpolatePrice(actualPrices[nextIndex - 1], next, date);
        }

        var last = actualPrices[^1];
        var previous = extrapolationPrices.Count > 1 ? extrapolationPrices[^2] : null;
        if (previous is null || last.Date == previous.Date)
        {
            return last.Value;
        }

        var elapsedDays = date.DayNumber - last.Date.DayNumber;
        var observedDays = last.Date.DayNumber - previous.Date.DayNumber;
        var dailyFactor = Math.Pow(last.Value / previous.Value, 1d / observedDays);
        return last.Value * Math.Pow(dailyFactor, elapsedDays);
    }

    private static double InterpolatePrice(PriceValue previous, PriceValue next, DateOnly date)
    {
        // Spread each observed close-to-close return over all calendar days, including weekends.
        var elapsedDays = date.DayNumber - previous.Date.DayNumber;
        var observedDays = next.Date.DayNumber - previous.Date.DayNumber;
        var dailyFactor = Math.Pow(next.Value / previous.Value, 1d / observedDays);
        return previous.Value * Math.Pow(dailyFactor, elapsedDays);
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
        new(date, returnPercent, returnPercent);
}
