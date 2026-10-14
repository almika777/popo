using Popo.Core.Portfolio;

namespace Popo.Core.PortfolioReturns;

internal static class PortfolioBenchmarkMath
{
    private const double Epsilon = 0.0000001d;

    public static double CalculateProfitPercent(
        DateOnly from,
        DateOnly to,
        double startValue,
        double endValue,
        IReadOnlyCollection<PortfolioCashFlowInput> flows)
    {
        var deposits = flows.Where(flow => flow.Type == PortfolioCashFlowType.Deposit && flow.Date <= to).Sum(flow => flow.Amount);
        var withdrawals = flows.Where(flow => flow.Type == PortfolioCashFlowType.Withdrawal && flow.Date <= to).Sum(flow => flow.Amount);
        var invested = startValue + deposits;
        if (!double.IsFinite(invested) || invested <= 0)
        {
            throw new InvalidOperationException("Сумма начального капитала и пополнений должна быть положительной.");
        }

        var profit = (endValue + withdrawals - startValue - deposits) / invested;
        if (!double.IsFinite(profit) || profit < -1)
        {
            throw new InvalidOperationException("Невозможно рассчитать денежную доходность для указанных данных.");
        }

        return profit;
    }

    public static IReadOnlyList<PortfolioBenchmarkPoint> BuildAssetPoints(
        DateOnly from,
        DateOnly to,
        AssetSimulation simulation,
        IReadOnlyCollection<PortfolioValuationInput> portfolioValuations,
        IReadOnlyCollection<PortfolioCashFlowInput> flows,
        double originalStartValue,
        DateOnly lineStartDate)
    {
        var dates = simulation.Prices.Where(price => price.Date > from && price.Date <= to).Select(price => price.Date)
            .Concat(portfolioValuations.Where(value => value.Date > from && value.Date <= to).Select(value => value.Date))
            .Concat(flows.Where(flow => flow.Date > from && flow.Date <= to).Select(flow => flow.Date))
            .Append(to)
            .Append(lineStartDate)
            .Where(date => date >= lineStartDate && date <= to)
            .Distinct()
            .OrderBy(date => date);
        var points = new List<PortfolioBenchmarkPoint>();
        foreach (var date in dates)
        {
            var scenarioValue = FindValuation(date, simulation.Valuations).TotalValue;
            var allFlowsThroughDate = flows.Where(flow => flow.Date <= date).ToArray();
            var profit = CalculateProfitPercent(from, date, originalStartValue, scenarioValue, allFlowsThroughDate);
            points.Add(new PortfolioBenchmarkPoint(date, profit, profit));
        }

        return points;
    }

    public static AssetSimulation SimulatePriceSeries(
        string assetName,
        DateOnly from,
        DateOnly to,
        double startValue,
        IReadOnlyList<PriceValue> prices,
        IReadOnlyCollection<PortfolioCashFlowInput> cashFlows,
        IReadOnlyCollection<DateOnly>? portfolioValuationDates = null,
        IReadOnlyCollection<PortfolioBondBenchmarkCouponPeriod>? couponPeriods = null)
    {
        ValidatePrices(assetName, prices);
        var startPrice = prices.LastOrDefault(value => value.Date <= from);
        var firstPriceInPeriod = prices.FirstOrDefault(value => value.Date > from && value.Date <= to);
        var endPrice = prices.LastOrDefault(value => value.Date <= to)
            ?? throw new PortfolioBenchmarkHistoryUnavailableException($"Нет исторической цены {assetName} на {to:yyyy-MM-dd} или раньше.");
        if (firstPriceInPeriod is null || endPrice.Date <= from)
        {
            throw new PortfolioBenchmarkHistoryUnavailableException($"В периоде с {from:yyyy-MM-dd} по {to:yyyy-MM-dd} нет торговых цен {assetName}.");
        }

        var priceByDate = prices.ToDictionary(value => value.Date);
        var flowsByDate = cashFlows.Where(flow => flow.Date > from && flow.Date <= to)
            .GroupBy(flow => flow.Date).ToDictionary(group => group.Key, group => group.ToArray());
        var couponInput = (couponPeriods ?? Array.Empty<PortfolioBondBenchmarkCouponPeriod>()).ToArray();
        var conflictingCoupon = couponInput.GroupBy(coupon => coupon.CouponDate)
            .FirstOrDefault(group => group.Distinct().Count() > 1);
        if (conflictingCoupon is not null)
        {
            throw new InvalidPortfolioBenchmarkHistoryException(
                $"В истории {assetName} есть противоречивые сведения о купоне за {conflictingCoupon.Key:yyyy-MM-dd}.");
        }

        couponInput = couponInput.Distinct().ToArray();
        var dates = prices.Where(price => price.Date > from && price.Date <= to).Select(price => price.Date)
            .Concat(flowsByDate.Keys)
            .Concat(couponInput.SelectMany(coupon => new[] { coupon.RecordDate, (DateOnly?)coupon.CouponDate }
                .Where(date => date.HasValue).Select(date => date!.Value)))
            .Concat(portfolioValuationDates ?? Array.Empty<DateOnly>())
            .Append(to).Where(date => date > from && date <= to).Distinct().OrderBy(date => date).ToArray();

        var hasInitialPrice = startPrice is not null;
        var units = hasInitialPrice ? startValue / startPrice!.Value : 0d;
        var uninvestedCash = hasInitialPrice ? 0d : startValue;
        var hasInvested = hasInitialPrice;
        var couponCash = 0d;
        var entitledUnitsByCouponDate = new Dictionary<DateOnly, double>();
        var couponPeriodsByRecordDate = couponInput
            .Where(coupon => coupon.RecordDate.HasValue && coupon.RecordDate.Value >= from && coupon.RecordDate.Value <= to)
            .GroupBy(coupon => coupon.RecordDate!.Value).ToDictionary(group => group.Key, group => group.ToArray());
        var couponPeriodsByPaymentDate = couponInput
            .Where(coupon => coupon.CouponDate > from && coupon.CouponDate <= to)
            .GroupBy(coupon => coupon.CouponDate).ToDictionary(group => group.Key, group => group.ToArray());
        foreach (var coupon in couponInput.Where(coupon => coupon.RecordDate == from))
        {
            entitledUnitsByCouponDate[coupon.CouponDate] = units;
        }

        double? currentPrice = startPrice?.Value;
        var values = new List<PortfolioValuationInput> { new(from, startValue) };
        foreach (var date in dates)
        {
            var hasPrice = priceByDate.TryGetValue(date, out var price);
            if (hasPrice)
            {
                currentPrice = price!.Value;
            }

            if (!hasInvested && hasPrice && price!.IsTradingDate)
            {
                units = uninvestedCash / currentPrice!.Value;
                uninvestedCash = 0;
                hasInvested = true;
            }

            if (couponPeriodsByRecordDate.TryGetValue(date, out var recordDateCoupons))
            {
                foreach (var coupon in recordDateCoupons)
                {
                    entitledUnitsByCouponDate[coupon.CouponDate] = units;
                }
            }

            if (couponPeriodsByPaymentDate.TryGetValue(date, out var paymentDateCoupons))
            {
                foreach (var coupon in paymentDateCoupons)
                {
                    if (!coupon.AmountInRub.HasValue || !double.IsFinite(coupon.AmountInRub.Value) || coupon.AmountInRub.Value <= 0)
                    {
                        throw new PortfolioBenchmarkHistoryUnavailableException(
                            $"Неизвестна сумма выплаты купона {assetName} за {coupon.CouponDate:yyyy-MM-dd}.");
                    }

                    if (entitledUnitsByCouponDate.TryGetValue(date, out var entitledUnits))
                    {
                        couponCash += entitledUnits * coupon.AmountInRub.Value;
                    }
                }
            }

            if (flowsByDate.TryGetValue(date, out var dateFlows))
            {
                var deposits = dateFlows.Where(flow => flow.Type == PortfolioCashFlowType.Deposit).Sum(flow => flow.Amount);
                var outflows = dateFlows.Where(flow => flow.Type is PortfolioCashFlowType.Withdrawal or PortfolioCashFlowType.Tax)
                    .Sum(flow => flow.Amount);
                var netFlow = deposits - outflows;

                if (!hasInvested)
                {
                    uninvestedCash += netFlow;
                    if (uninvestedCash < -Epsilon)
                    {
                        throw new PortfolioBenchmarkHistoryUnavailableException(
                            $"Сценарий {assetName} не может покрыть денежные операции от {date:yyyy-MM-dd} до первой цены.");
                    }
                }
                else if (netFlow < -Epsilon)
                {
                    var outflowAfterCoupons = Math.Max(0, -netFlow - couponCash);
                    couponCash = Math.Max(0, couponCash + netFlow);
                    var unitsToSell = outflowAfterCoupons / currentPrice!.Value;
                    if (unitsToSell > units + Epsilon)
                    {
                        throw new PortfolioBenchmarkHistoryUnavailableException(
                            $"Сценарий {assetName} не может покрыть денежные операции от {date:yyyy-MM-dd} при тех же потоках.");
                    }

                    units = Math.Max(0, units - unitsToSell);
                }
                else if (netFlow > Epsilon)
                {
                    if (!currentPrice.HasValue)
                    {
                        throw new PortfolioBenchmarkHistoryUnavailableException(
                            $"Нет цены {assetName} для денежного потока от {date:yyyy-MM-dd}.");
                    }

                    units += netFlow / currentPrice.Value;
                }
            }

            var value = units * (currentPrice ?? 0d) + uninvestedCash + couponCash;
            if (!double.IsFinite(value) || (date == to && value < -Epsilon))
            {
                throw new PortfolioBenchmarkHistoryUnavailableException(
                    $"Сценарий {assetName} не может покрыть вывод от {date:yyyy-MM-dd} при тех же денежных потоках.");
            }

            values.Add(new PortfolioValuationInput(date, Math.Max(0, value)));
        }

        return new AssetSimulation((startPrice ?? firstPriceInPeriod).Date, endPrice.Date, prices, values);
    }

    public static void EnsureFlowDateValuations(
        IReadOnlyCollection<PortfolioCashFlowInput> flows,
        IReadOnlyCollection<PortfolioValuationInput> valuations)
    {
        var missingDates = flows.Select(flow => flow.Date).Distinct()
            .Where(date => valuations.Count(value => value.Date == date) != 1).OrderBy(date => date).ToArray();
        if (missingDates.Length > 0)
        {
            throw new MissingPortfolioBenchmarkValuationsException(missingDates);
        }
    }

    public static PortfolioValuationInput FindValuation(DateOnly date, IReadOnlyCollection<PortfolioValuationInput> valuations) =>
        valuations.SingleOrDefault(value => value.Date == date)
        ?? throw new InvalidOperationException($"Укажите оценку портфеля на дату {date:dd.MM.yyyy}.");

    public static double GetExternalCashMovement(PortfolioCashFlowInput flow) => flow.Type switch
    {
        PortfolioCashFlowType.Deposit => flow.Amount,
        PortfolioCashFlowType.Withdrawal => -flow.Amount,
        PortfolioCashFlowType.Tax => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(flow))
    };

    private static void ValidatePrices(string assetName, IReadOnlyList<PriceValue> prices)
    {
        var duplicate = prices.GroupBy(price => price.Date).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidPortfolioBenchmarkHistoryException($"В истории {assetName} несколько цен за {duplicate.Key:yyyy-MM-dd}.");
        }

        if (prices.Any(value => !double.IsFinite(value.Value) || value.Value <= 0))
        {
            throw new InvalidPortfolioBenchmarkHistoryException($"В истории {assetName} найдено некорректное значение цены.");
        }
    }
}

internal sealed record PriceValue(DateOnly Date, double Value, bool IsTradingDate);

internal sealed record AssetSimulation(
    DateOnly StartPriceDate,
    DateOnly EndPriceDate,
    IReadOnlyList<PriceValue> Prices,
    IReadOnlyList<PortfolioValuationInput> Valuations);
