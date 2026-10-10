namespace Popo.Core.PortfolioReturns;

internal sealed class Ru000A10F504BenchmarkSeriesCalculator
{
    private const string Isin = "RU000A10F504";
    private const double InitialCapital = 1_000_000d;

    public IndexedBondBenchmarkSeriesCalculation Calculate(
        DateOnly from,
        DateOnly to,
        PortfolioBondBenchmarkHistory? history)
    {
        DateOnly? comparisonStartDate = null;
        DateOnly? startObservationDate = null;
        DateOnly? endObservationDate = null;

        try
        {
            if (history is null)
            {
                throw new PortfolioBenchmarkHistoryUnavailableException($"В хранилище нет истории {Isin}.");
            }

            comparisonStartDate = FindComparisonStartDate(from, to, history);
            var comparisonStart = comparisonStartDate.Value;
            var prices = BuildDailyPrices(from, to, history);
            var simulation = PortfolioBenchmarkMath.SimulatePriceSeries(
                Isin, from, to, InitialCapital, prices, [],
                couponPeriods: history.CouponPeriods);

            var localTwr = PortfolioBenchmarkMath.CalculateTwrPercent(
                from, to, simulation.Valuations, []);
            var endValue = PortfolioBenchmarkMath.FindValuation(to, simulation.Valuations).TotalValue;
            var profit = PortfolioBenchmarkMath.CalculateProfitPercent(from, to, InitialCapital, endValue, []);

            startObservationDate = history.Points.Where(point => point.Date >= from && point.Date <= to)
                .MinBy(point => point.Date)?.Date;
            endObservationDate = history.Points.Where(point => point.Date <= to)
                .MaxBy(point => point.Date)?.Date;
            var points = PortfolioBenchmarkMath.BuildAssetPoints(
                from, to, simulation, [], [], InitialCapital, comparisonStart);

            return new IndexedBondBenchmarkSeriesCalculation(
                comparisonStartDate, startObservationDate, endObservationDate,
                new BenchmarkSeriesCalculation(profit, localTwr, points), null);
        }
        catch (PortfolioBenchmarkHistoryUnavailableException exception)
        {
            return Unavailable(exception.Message);
        }
        catch (InvalidPortfolioBenchmarkHistoryException exception)
        {
            return Unavailable(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Unavailable($"Не удалось рассчитать сценарий {Isin}: {exception.Message}");
        }

        IndexedBondBenchmarkSeriesCalculation Unavailable(string reason) =>
            new(comparisonStartDate, startObservationDate, endObservationDate, null, reason);
    }

    private static IReadOnlyList<PriceValue> BuildDailyPrices(
        DateOnly from,
        DateOnly to,
        PortfolioBondBenchmarkHistory history)
    {
        if (!string.Equals(history.Isin, Isin, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidPortfolioBenchmarkHistoryException($"История облигации не соответствует {Isin}.");
        }

        if (!IsRubleUnit(history.CurrencyId) || !IsRubleUnit(history.FaceUnit))
        {
            throw new PortfolioBenchmarkHistoryUnavailableException($"Для {Isin} поддерживается расчёт только рублёвой бумаги с номиналом в рублях.");
        }

        var points = history.Points.OrderBy(point => point.Date).ToArray();
        if (points.Length == 0)
        {
            throw new PortfolioBenchmarkHistoryUnavailableException($"В хранилище нет дневной истории {Isin}.");
        }

        if (points.GroupBy(point => point.Date).Any(group => group.Count() > 1))
        {
            throw new InvalidPortfolioBenchmarkHistoryException($"В истории {Isin} есть повторяющиеся даты.");
        }

        if (history.CouponPeriods.Count == 0 && points.Any(point => point.CouponPercent > 0))
        {
            throw new PortfolioBenchmarkHistoryUnavailableException(
                $"Нет расписания купонов {Isin}; невозможно подтвердить отсутствие выплат в выбранном периоде.");
        }

        foreach (var coupon in history.CouponPeriods.Where(coupon => coupon.CouponDate > from && coupon.CouponDate <= to))
        {
            if (!coupon.RecordDate.HasValue)
            {
                throw new PortfolioBenchmarkHistoryUnavailableException(
                    $"Неизвестна дата фиксации владельцев {Isin} для выплаты {coupon.CouponDate:yyyy-MM-dd}.");
            }

            if (coupon.AmountInRub is not > 0 || !double.IsFinite(coupon.AmountInRub.Value))
            {
                throw new PortfolioBenchmarkHistoryUnavailableException(
                    $"Неизвестна сумма выплаты купона {Isin} за {coupon.CouponDate:yyyy-MM-dd}.");
            }
        }

        var startPoint = points.FirstOrDefault(point => point.Date >= from && point.Date <= to)
            ?? throw new PortfolioBenchmarkHistoryUnavailableException($"Нет цены {Isin} в периоде с {from:yyyy-MM-dd} по {to:yyyy-MM-dd}.");
        var observationIndexForCoverage = 0;
        var coverageTerms = startPoint;
        for (var date = startPoint.Date; date <= to; date = date.AddDays(1))
        {
            while (observationIndexForCoverage < points.Length && points[observationIndexForCoverage].Date <= date)
            {
                coverageTerms = points[observationIndexForCoverage++];
            }

            if (coverageTerms.CouponPercent > 0 && !history.CouponPeriods.Any(period =>
                    period.PeriodStartDate <= date && period.CouponDate >= date))
            {
                throw new PortfolioBenchmarkHistoryUnavailableException(
                    $"Расписание купонов {Isin} не покрывает {date:yyyy-MM-dd}; сценарий нельзя рассчитать точно.");
            }
        }

        var observations = points.Where(point => point.Date >= startPoint.Date && point.Date <= to).ToArray();
        var priceMap = new Dictionary<DateOnly, double>();
        var currentFace = startPoint.FaceValue;
        var currentCoupon = startPoint.CouponPercent;
        var currentCleanPrice = GetCleanPrice(startPoint);
        var accruedInterest = GetAccruedInterest(startPoint);
        var couponsByDate = history.CouponPeriods.Select(coupon => coupon.CouponDate).ToHashSet();
        var observationByDate = observations.ToDictionary(point => point.Date);
        var actualTradeDates = new HashSet<DateOnly>();
        for (var date = startPoint.Date; date <= to; date = date.AddDays(1))
        {
            if (observationByDate.TryGetValue(date, out var observed))
            {
                currentFace = observed.FaceValue;
                currentCoupon = observed.CouponPercent;
                currentCleanPrice = GetCleanPrice(observed);
                accruedInterest = GetAccruedInterest(observed);
                actualTradeDates.Add(date);
            }
            else if (couponsByDate.Contains(date))
            {
                accruedInterest = 0;
            }
            else if (date > startPoint.Date)
            {
                accruedInterest += currentFace * currentCoupon / 100d / 365d;
            }

            var dirtyValue = currentCleanPrice * currentFace / 100d + accruedInterest;
            if (!double.IsFinite(dirtyValue) || dirtyValue <= 0)
            {
                throw new InvalidPortfolioBenchmarkHistoryException($"Некорректная грязная цена {Isin} за {date:yyyy-MM-dd}.");
            }

            priceMap[date] = dirtyValue;
        }

        if (!actualTradeDates.Any(date => date > from && date <= to))
        {
            throw new PortfolioBenchmarkHistoryUnavailableException($"В периоде с {from:yyyy-MM-dd} по {to:yyyy-MM-dd} нет торговых цен {Isin}.");
        }

        return priceMap.Select(pair => new PriceValue(pair.Key, pair.Value, actualTradeDates.Contains(pair.Key))).ToArray();
    }

    private static double GetCleanPrice(PortfolioBondBenchmarkPricePoint point)
    {
        var close = point.LegalClosePricePercent ?? point.ClosePricePercent;
        if (close is not > 0 || !double.IsFinite(close.Value) || !double.IsFinite(point.FaceValue) || point.FaceValue <= 0 ||
            !double.IsFinite(point.CouponPercent) || point.CouponPercent < 0)
        {
            throw new PortfolioBenchmarkHistoryUnavailableException($"Для {Isin} отсутствует корректная цена, номинал или купон за {point.Date:yyyy-MM-dd}.");
        }

        return close.Value;
    }

    private static double GetAccruedInterest(PortfolioBondBenchmarkPricePoint point)
    {
        if (point.AccruedInterest is null || !double.IsFinite(point.AccruedInterest.Value) || point.AccruedInterest.Value < 0)
        {
            throw new PortfolioBenchmarkHistoryUnavailableException($"Для {Isin} отсутствует фактический НКД за торговую дату {point.Date:yyyy-MM-dd}.");
        }

        return point.AccruedInterest.Value;
    }

    private static DateOnly FindComparisonStartDate(
        DateOnly from,
        DateOnly to,
        PortfolioBondBenchmarkHistory history)
    {
        if (history.Points.Any(point => point.Date == from))
        {
            return from;
        }

        var firstPrice = history.Points.Where(point => point.Date > from && point.Date <= to).MinBy(point => point.Date)
            ?? throw new PortfolioBenchmarkHistoryUnavailableException($"Нет цены {Isin} в выбранном периоде.");
        return firstPrice.Date;
    }

    private static bool IsRubleUnit(string? unit) => string.Equals(unit, "RUB", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(unit, "SUR", StringComparison.OrdinalIgnoreCase);
}
