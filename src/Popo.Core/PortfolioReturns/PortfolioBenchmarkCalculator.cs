using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;

namespace Popo.Core.PortfolioReturns;

public sealed class PortfolioBenchmarkCalculator
{
    private const string LqdtSecId = "LQDT";
    private const string IndexedBondIsin = "RU000A10F504";
    private const double Epsilon = 0.0000001d;

    public PortfolioBenchmarkResult Calculate(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<PortfolioValuationInput> valuations,
        IReadOnlyCollection<PortfolioCashFlowInput> cashFlows,
        IReadOnlyCollection<MoexIndexHistory> lqdtPriceHistory,
        PortfolioBondBenchmarkHistory? indexedBondHistory = null)
    {
        ArgumentNullException.ThrowIfNull(valuations);
        ArgumentNullException.ThrowIfNull(cashFlows);
        ArgumentNullException.ThrowIfNull(lqdtPriceHistory);
        if (to <= from)
        {
            throw new ArgumentException("Конечная дата должна быть позже начальной.", nameof(to));
        }

        var periodValuations = GetPeriodValuations(from, to, valuations);
        var startValue = FindValuation(from, periodValuations).TotalValue;
        if (startValue <= 0)
        {
            throw new ArgumentException("Стоимость портфеля на начало периода должна быть больше нуля.", nameof(valuations));
        }

        var periodFlows = cashFlows.Where(flow => flow.Date > from && flow.Date <= to).OrderBy(flow => flow.Date).ToArray();
        ValidateCashFlows(periodFlows);
        var adjustedValuations = AddTaxReserve(from, periodValuations, periodFlows);
        var externalFlows = periodFlows.Where(flow => flow.Type != PortfolioCashFlowType.Tax).ToArray();
        EnsureFlowDateValuations(externalFlows, adjustedValuations);

        var portfolioEndValue = FindValuation(to, adjustedValuations).TotalValue;
        var portfolioProfit = CalculateProfitPercent(from, to, startValue, portfolioEndValue, externalFlows);
        var portfolioTwr = CalculateTwrPercent(from, to, adjustedValuations, externalFlows);
        var portfolioPoints = BuildPortfolioPoints(from, adjustedValuations, externalFlows);

        var lqdtSimulation = SimulateLqdtPortfolio(from, to, startValue, adjustedValuations, externalFlows, lqdtPriceHistory);
        var lqdtProfit = CalculateProfitPercent(from, to, startValue,
            FindValuation(to, lqdtSimulation.Valuations).TotalValue, externalFlows);
        var lqdtTwr = CalculateTwrPercent(from, to, lqdtSimulation.Valuations, externalFlows);
        var lqdtPoints = BuildAssetPoints(from, to, lqdtSimulation, adjustedValuations, externalFlows,
            startValue, includeOnlyFrom: null, twrPrefix: 0);

        double? indexedBondProfit = null;
        double? indexedBondTwr = null;
        double? indexedBondLegacyReturn = null;
        double? indexedBondPortfolioLegacyReturn = null;
        DateOnly? indexedBondComparisonStartDate = null;
        DateOnly? indexedBondStartObservationDate = null;
        DateOnly? indexedBondEndObservationDate = null;
        IReadOnlyList<PortfolioBenchmarkPoint>? indexedBondPoints = null;
        string? indexedBondUnavailableReason = null;

        try
        {
            if (indexedBondHistory is null)
            {
                throw new PortfolioBenchmarkHistoryUnavailableException($"В хранилище нет истории {IndexedBondIsin}.");
            }

            indexedBondComparisonStartDate = FindIndexedBondComparisonStartDate(from, to, indexedBondHistory, periodValuations);
            var comparisonStart = indexedBondComparisonStartDate.Value;
            var bondPrices = BuildIndexedBondDailyPrices(comparisonStart, to, indexedBondHistory);
            var hybridStartValue = FindValuation(comparisonStart, adjustedValuations).TotalValue;
            var bondFlows = externalFlows.Where(flow => flow.Date > comparisonStart).ToArray();
            var bondSimulation = SimulatePriceSeries(IndexedBondIsin, comparisonStart, to, hybridStartValue,
                bondPrices, bondFlows, deferInitialPurchase: comparisonStart > from,
                couponPeriods: indexedBondHistory.CouponPeriods);

            double? portfolioPrefixTwr = comparisonStart == from
                ? 0d
                : CalculateTwrPercent(from, comparisonStart,
                    adjustedValuations.Where(value => value.Date <= comparisonStart).ToArray(),
                    externalFlows.Where(flow => flow.Date <= comparisonStart).ToArray());
            var localTwr = CalculateTwrPercent(comparisonStart, to, bondSimulation.Valuations, bondFlows);
            indexedBondTwr = localTwr is null || portfolioPrefixTwr is null
                ? null
                : (1 + portfolioPrefixTwr.Value) * (1 + localTwr.Value) - 1;
            indexedBondProfit = CalculateProfitPercent(from, to, startValue,
                FindValuation(to, bondSimulation.Valuations).TotalValue, externalFlows);

            indexedBondLegacyReturn = indexedBondProfit;
            indexedBondPortfolioLegacyReturn = comparisonStart == from
                ? portfolioTwr
                : CalculateTwrPercent(comparisonStart, to,
                    adjustedValuations.Where(value => value.Date >= comparisonStart).ToArray(),
                    externalFlows.Where(flow => flow.Date > comparisonStart).ToArray());
            indexedBondStartObservationDate = indexedBondHistory.Points.Where(point => point.Date <= comparisonStart)
                .MaxBy(point => point.Date)?.Date;
            indexedBondEndObservationDate = indexedBondHistory.Points.Where(point => point.Date <= to)
                .MaxBy(point => point.Date)?.Date;
            indexedBondPoints = BuildAssetPoints(comparisonStart, to, bondSimulation,
                adjustedValuations.Where(value => value.Date >= comparisonStart).ToArray(), externalFlows,
                startValue, comparisonStart, portfolioPrefixTwr);
        }
        catch (PortfolioBenchmarkHistoryUnavailableException exception)
        {
            indexedBondProfit = null;
            indexedBondTwr = null;
            indexedBondLegacyReturn = null;
            indexedBondPortfolioLegacyReturn = null;
            indexedBondPoints = null;
            indexedBondUnavailableReason = exception.Message;
        }
        catch (InvalidPortfolioBenchmarkHistoryException exception)
        {
            indexedBondProfit = null;
            indexedBondTwr = null;
            indexedBondLegacyReturn = null;
            indexedBondPortfolioLegacyReturn = null;
            indexedBondPoints = null;
            indexedBondUnavailableReason = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            indexedBondProfit = null;
            indexedBondTwr = null;
            indexedBondLegacyReturn = null;
            indexedBondPortfolioLegacyReturn = null;
            indexedBondPoints = null;
            indexedBondUnavailableReason = $"Не удалось рассчитать сценарий {IndexedBondIsin}: {exception.Message}";
        }

        return new PortfolioBenchmarkResult(
            from, to, portfolioProfit, lqdtProfit, portfolioProfit - lqdtProfit,
            lqdtSimulation.StartPriceDate, lqdtSimulation.EndPriceDate,
            portfolioPoints, lqdtPoints,
            indexedBondLegacyReturn, indexedBondStartObservationDate, indexedBondEndObservationDate,
            indexedBondPoints, indexedBondUnavailableReason, indexedBondComparisonStartDate,
            indexedBondPortfolioLegacyReturn,
            portfolioProfit, lqdtProfit, indexedBondProfit,
            portfolioTwr, lqdtTwr, indexedBondTwr);
    }

    private static IReadOnlyList<PortfolioBenchmarkPoint> BuildPortfolioPoints(
        DateOnly from,
        IReadOnlyCollection<PortfolioValuationInput> valuations,
        IReadOnlyCollection<PortfolioCashFlowInput> flows)
    {
        var start = FindValuation(from, valuations).TotalValue;
        var points = new List<PortfolioBenchmarkPoint> { new(from, 0, 0, 0) };
        foreach (var value in valuations.Where(value => value.Date > from).OrderBy(value => value.Date))
        {
            var pointFlows = flows.Where(flow => flow.Date <= value.Date).ToArray();
            var profit = CalculateProfitPercent(from, value.Date, start, value.TotalValue, pointFlows);
            var twr = CalculateTwrPercent(from, value.Date,
                valuations.Where(item => item.Date <= value.Date).ToArray(), pointFlows);
            points.Add(new PortfolioBenchmarkPoint(value.Date, profit, profit, twr));
        }

        return points;
    }

    private static AssetSimulation SimulateLqdtPortfolio(
        DateOnly from,
        DateOnly to,
        double startValue,
        IReadOnlyCollection<PortfolioValuationInput> portfolioValuations,
        IReadOnlyCollection<PortfolioCashFlowInput> cashFlows,
        IReadOnlyCollection<MoexIndexHistory> history)
    {
        var prices = history.Where(value => string.Equals(value.SecId, LqdtSecId, StringComparison.Ordinal))
            .Select(ToPrice).OrderBy(value => value.Date).ToArray();
        return SimulatePriceSeries(LqdtSecId, from, to, startValue, prices, cashFlows, deferInitialPurchase: false,
            portfolioValuationDates: portfolioValuations.Select(value => value.Date).ToArray());
    }

    private static AssetSimulation SimulatePriceSeries(
        string assetName,
        DateOnly from,
        DateOnly to,
        double startValue,
        IReadOnlyList<PriceValue> prices,
        IReadOnlyCollection<PortfolioCashFlowInput> cashFlows,
        bool deferInitialPurchase,
        IReadOnlyCollection<DateOnly>? portfolioValuationDates = null,
        IReadOnlyCollection<PortfolioBondBenchmarkCouponPeriod>? couponPeriods = null)
    {
        ValidatePrices(assetName, prices);
        var startPrice = prices.LastOrDefault(value => value.Date <= from)
            ?? throw new PortfolioBenchmarkHistoryUnavailableException($"Нет исторической цены {assetName} на {from:yyyy-MM-dd} или раньше.");
        var endPrice = prices.LastOrDefault(value => value.Date <= to)
            ?? throw new PortfolioBenchmarkHistoryUnavailableException($"Нет исторической цены {assetName} на {to:yyyy-MM-dd} или раньше.");
        if (endPrice.Date <= from)
        {
            throw new PortfolioBenchmarkHistoryUnavailableException($"В периоде с {from:yyyy-MM-dd} по {to:yyyy-MM-dd} нет торговых цен {assetName}.");
        }

        var priceByDate = prices.ToDictionary(value => value.Date);
        var flowsByDate = cashFlows.GroupBy(flow => flow.Date).ToDictionary(group => group.Key, group => group.ToArray());
        var couponInput = (couponPeriods ?? Array.Empty<PortfolioBondBenchmarkCouponPeriod>()).ToArray();
        var conflictingCoupon = couponInput.GroupBy(coupon => coupon.CouponDate)
            .FirstOrDefault(group => group.Distinct().Count() > 1);
        if (conflictingCoupon is not null)
        {
            throw new InvalidPortfolioBenchmarkHistoryException(
                $"В истории {IndexedBondIsin} есть противоречивые сведения о купоне за {conflictingCoupon.Key:yyyy-MM-dd}.");
        }

        couponInput = couponInput.Distinct().ToArray();
        var dates = prices.Where(price => price.Date > from && price.Date <= to).Select(price => price.Date)
            .Concat(flowsByDate.Keys)
            .Concat(couponInput.SelectMany(coupon => new[] { coupon.RecordDate, (DateOnly?)coupon.CouponDate }
                .Where(date => date.HasValue).Select(date => date!.Value)) ?? Array.Empty<DateOnly>())
            .Concat(portfolioValuationDates ?? Array.Empty<DateOnly>())
            .Append(to).Where(date => date > from && date <= to).Distinct().OrderBy(date => date).ToArray();

        var units = deferInitialPurchase ? 0d : startValue / startPrice.Value;
        var pendingFlowCash = deferInitialPurchase ? startValue : 0d;
        var couponCash = 0d;
        var entitledUnitsByCouponDate = new Dictionary<DateOnly, double>();
        var couponPeriodsByRecordDate = couponInput
            .Where(coupon => coupon.RecordDate.HasValue && coupon.RecordDate.Value >= from && coupon.RecordDate.Value <= to)
            .GroupBy(coupon => coupon.RecordDate!.Value).ToDictionary(group => group.Key, group => group.ToArray());
        var couponPeriodsByPaymentDate = couponInput
            .Where(coupon => coupon.CouponDate > from && coupon.CouponDate <= to)
            .GroupBy(coupon => coupon.CouponDate).ToDictionary(group => group.Key, group => group.ToArray());
        foreach (var coupon in couponInput
                     .Where(coupon => coupon.RecordDate == from))
        {
            entitledUnitsByCouponDate[coupon.CouponDate] = units;
        }
        var currentPrice = startPrice.Value;
        var values = new List<PortfolioValuationInput> { new(from, startValue) };
        foreach (var date in dates)
        {
            var hasPrice = priceByDate.TryGetValue(date, out var price);
            if (hasPrice)
            {
                currentPrice = price!.Value;
            }

            if (hasPrice && price!.IsTradingDate)
            {
                if (Math.Abs(pendingFlowCash) > Epsilon)
                {
                    if (pendingFlowCash < 0)
                    {
                        var unitsToSell = -pendingFlowCash / currentPrice;
                        if (unitsToSell > units + Epsilon)
                        {
                            throw new PortfolioBenchmarkHistoryUnavailableException(
                                $"Сценарий {assetName} не может покрыть вывод от {date:yyyy-MM-dd} при тех же денежных потоках.");
                        }

                        units = Math.Max(0, units - unitsToSell);
                    }
                    else
                    {
                        units += pendingFlowCash / currentPrice;
                    }

                    pendingFlowCash = 0;
                }
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
                            $"Неизвестна сумма выплаты купона {IndexedBondIsin} за {coupon.CouponDate:yyyy-MM-dd}.");
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
                var withdrawals = dateFlows.Where(flow => flow.Type == PortfolioCashFlowType.Withdrawal).Sum(flow => flow.Amount);
                var cashWithdrawal = Math.Min(couponCash, withdrawals);
                couponCash -= cashWithdrawal;
                pendingFlowCash += deposits - (withdrawals - cashWithdrawal);
            }

            var value = units * currentPrice + pendingFlowCash + couponCash;
            if (!double.IsFinite(value) || (date == to && value < -Epsilon))
            {
                throw new PortfolioBenchmarkHistoryUnavailableException(
                    $"Сценарий {assetName} не может покрыть вывод от {date:yyyy-MM-dd} при тех же денежных потоках.");
            }

            values.Add(new PortfolioValuationInput(date, Math.Max(0, value)));
        }

        return new AssetSimulation(startPrice.Date, endPrice.Date, prices, values);
    }

    private static IReadOnlyList<PortfolioBenchmarkPoint> BuildAssetPoints(
        DateOnly from,
        DateOnly to,
        AssetSimulation simulation,
        IReadOnlyCollection<PortfolioValuationInput> portfolioValuations,
        IReadOnlyCollection<PortfolioCashFlowInput> flows,
        double originalStartValue,
        DateOnly? includeOnlyFrom,
        double? twrPrefix)
    {
        var startValue = FindValuation(from, simulation.Valuations).TotalValue;
        var dates = simulation.Prices.Where(price => price.Date > from && price.Date <= to).Select(price => price.Date)
            .Concat(portfolioValuations.Where(value => value.Date > from && value.Date <= to).Select(value => value.Date))
            .Append(to).Distinct().OrderBy(date => date);
        var points = new List<PortfolioBenchmarkPoint>();
        if (includeOnlyFrom is null)
        {
            points.Add(new PortfolioBenchmarkPoint(from, 0, 0, 0));
        }
        else
        {
            var joinFlows = flows.Where(flow => flow.Date <= from).ToArray();
            var joinProfit = CalculateProfitPercent(from, from, originalStartValue, startValue, joinFlows);
            points.Add(new PortfolioBenchmarkPoint(from, joinProfit, joinProfit, twrPrefix));
        }

        foreach (var date in dates)
        {
            var scenarioValue = FindValuation(date, simulation.Valuations).TotalValue;
            var allFlowsThroughDate = flows.Where(flow => flow.Date <= date).ToArray();
            var profit = CalculateProfitPercent(from, date, originalStartValue, scenarioValue, allFlowsThroughDate);
            var localTwr = CalculateTwrPercent(from, date,
                simulation.Valuations.Where(value => value.Date <= date).ToArray(), allFlowsThroughDate);
            double? twr = includeOnlyFrom is null
                ? localTwr
                : localTwr is null || twrPrefix is null
                    ? null
                    : (1 + twrPrefix.Value) * (1 + localTwr.Value) - 1;
            points.Add(new PortfolioBenchmarkPoint(date, profit, profit, twr));
        }

        return points;
    }

    private static double CalculateProfitPercent(
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

    private static double? CalculateTwrPercent(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<PortfolioValuationInput> valuations,
        IReadOnlyCollection<PortfolioCashFlowInput> flows)
    {
        if (to <= from)
        {
            return 0;
        }

        var start = FindValuation(from, valuations).TotalValue;
        var flowGroups = flows.Where(flow => flow.Date > from && flow.Date <= to)
            .GroupBy(flow => flow.Date).ToDictionary(group => group.Key, group => group.ToArray());
        EnsureFlowDateValuations(flows.Where(flow => flow.Date > from && flow.Date <= to).ToArray(), valuations);

        var multiplier = 1d;
        var capital = start;
        foreach (var valuation in valuations.Where(value => value.Date > from && value.Date <= to).OrderBy(value => value.Date))
        {
            var netFlow = flowGroups.TryGetValue(valuation.Date, out var dayFlows)
                ? dayFlows.Sum(GetExternalCashMovement)
                : 0d;
            if (flowGroups.ContainsKey(valuation.Date))
            {
                if (capital <= 0)
                {
                    return null;
                }

                var preFlowValue = valuation.TotalValue - netFlow;
                if (!double.IsFinite(preFlowValue) || preFlowValue < 0)
                {
                    return null;
                }

                multiplier *= preFlowValue / capital;
                capital = valuation.TotalValue;
            }
            else if (capital > 0)
            {
                multiplier *= valuation.TotalValue / capital;
                capital = valuation.TotalValue;
            }
            else if (valuation.TotalValue > 0)
            {
                return null;
            }
        }

        var result = multiplier - 1;
        return double.IsFinite(result) && result >= -1 ? result : null;
    }

    private static IReadOnlyList<PortfolioValuationInput> AddTaxReserve(
        DateOnly from,
        IReadOnlyCollection<PortfolioValuationInput> valuations,
        IReadOnlyCollection<PortfolioCashFlowInput> flows) => valuations.Select(value =>
        {
            var taxReserve = flows.Where(flow => flow.Type == PortfolioCashFlowType.Tax && flow.Date > from && flow.Date <= value.Date)
                .Sum(flow => flow.Amount);
            var adjustedValue = value.TotalValue + taxReserve;
            if (!double.IsFinite(adjustedValue))
            {
                throw new InvalidOperationException($"Не удалось добавить налоговый резерв к оценке за {value.Date:yyyy-MM-dd}.");
            }

            return value with { TotalValue = adjustedValue };
        }).ToArray();

    private static void EnsureFlowDateValuations(
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

    private static IReadOnlyCollection<PortfolioValuationInput> GetPeriodValuations(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<PortfolioValuationInput> valuations)
    {
        var duplicate = valuations.GroupBy(value => value.Date).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Для даты {duplicate.Key:dd.MM.yyyy} допускается только одна оценка портфеля.");
        }

        if (valuations.Any(value => !double.IsFinite(value.TotalValue) || value.TotalValue < 0))
        {
            throw new ArgumentException("Стоимость портфеля должна быть неотрицательной.", nameof(valuations));
        }

        var period = valuations.Where(value => value.Date >= from && value.Date <= to).OrderBy(value => value.Date).ToArray();
        _ = FindValuation(from, period);
        _ = FindValuation(to, period);
        return period;
    }

    private static PortfolioValuationInput FindValuation(DateOnly date, IReadOnlyCollection<PortfolioValuationInput> valuations) =>
        valuations.SingleOrDefault(value => value.Date == date)
        ?? throw new InvalidOperationException($"Укажите оценку портфеля на дату {date:dd.MM.yyyy}.");

    private static void ValidateCashFlows(IEnumerable<PortfolioCashFlowInput> flows)
    {
        foreach (var flow in flows)
        {
            if (!double.IsFinite(flow.Amount) || flow.Amount <= 0)
            {
                throw new ArgumentException("Сумма денежной операции должна быть положительной.", nameof(flows));
            }
        }
    }

    private static double GetExternalCashMovement(PortfolioCashFlowInput flow) => flow.Type switch
    {
        PortfolioCashFlowType.Deposit => flow.Amount,
        PortfolioCashFlowType.Withdrawal => -flow.Amount,
        PortfolioCashFlowType.Tax => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(flow))
    };

    private static PriceValue ToPrice(MoexIndexHistory value)
    {
        if (value.TradeDate is null || value.Close is null || value.Close <= 0)
        {
            throw new InvalidPortfolioBenchmarkHistoryException("В истории MOEX найдено некорректное значение цены LQDT.");
        }

        return new PriceValue(value.TradeDate.Value, (double)value.Close.Value, true);
    }

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

    private static IReadOnlyList<PriceValue> BuildIndexedBondDailyPrices(
        DateOnly from,
        DateOnly to,
        PortfolioBondBenchmarkHistory history)
    {
        if (!string.Equals(history.Isin, IndexedBondIsin, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidPortfolioBenchmarkHistoryException($"История облигации не соответствует {IndexedBondIsin}.");
        }

        if (!IsRubleUnit(history.CurrencyId) || !IsRubleUnit(history.FaceUnit))
        {
            throw new PortfolioBenchmarkHistoryUnavailableException($"Для {IndexedBondIsin} поддерживается расчёт только рублёвой бумаги с номиналом в рублях.");
        }

        var points = history.Points.OrderBy(point => point.Date).ToArray();
        if (points.Length == 0)
        {
            throw new PortfolioBenchmarkHistoryUnavailableException($"В хранилище нет дневной истории {IndexedBondIsin}.");
        }

        if (points.GroupBy(point => point.Date).Any(group => group.Count() > 1))
        {
            throw new InvalidPortfolioBenchmarkHistoryException($"В истории {IndexedBondIsin} есть повторяющиеся даты.");
        }

        if (history.CouponPeriods.Count == 0 && points.Any(point => point.CouponPercent > 0))
        {
            throw new PortfolioBenchmarkHistoryUnavailableException(
                $"Нет расписания купонов {IndexedBondIsin}; невозможно подтвердить отсутствие выплат в выбранном периоде.");
        }

        foreach (var coupon in history.CouponPeriods.Where(coupon => coupon.CouponDate > from && coupon.CouponDate <= to))
        {
            if (!coupon.RecordDate.HasValue)
            {
                throw new PortfolioBenchmarkHistoryUnavailableException(
                    $"Неизвестна дата фиксации владельцев {IndexedBondIsin} для выплаты {coupon.CouponDate:yyyy-MM-dd}.");
            }

            if (coupon.AmountInRub is not > 0 || !double.IsFinite(coupon.AmountInRub.Value))
            {
                throw new PortfolioBenchmarkHistoryUnavailableException(
                    $"Неизвестна сумма выплаты купона {IndexedBondIsin} за {coupon.CouponDate:yyyy-MM-dd}.");
            }
        }

        var startPoint = points.LastOrDefault(point => point.Date <= from)
            ?? points.FirstOrDefault(point => point.Date > from && point.Date <= to)
            ?? throw new PortfolioBenchmarkHistoryUnavailableException($"Нет цены {IndexedBondIsin} в периоде с {from:yyyy-MM-dd} по {to:yyyy-MM-dd}.");
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
                    $"Расписание купонов {IndexedBondIsin} не покрывает {date:yyyy-MM-dd}; сценарий нельзя рассчитать точно.");
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
                throw new InvalidPortfolioBenchmarkHistoryException($"Некорректная грязная цена {IndexedBondIsin} за {date:yyyy-MM-dd}.");
            }

            priceMap[date] = dirtyValue;
        }

        if (!actualTradeDates.Any(date => date > from && date <= to))
        {
            throw new PortfolioBenchmarkHistoryUnavailableException($"В периоде с {from:yyyy-MM-dd} по {to:yyyy-MM-dd} нет торговых цен {IndexedBondIsin}.");
        }

        return priceMap.Select(pair => new PriceValue(pair.Key, pair.Value, actualTradeDates.Contains(pair.Key))).ToArray();
    }

    private static double GetCleanPrice(PortfolioBondBenchmarkPricePoint point)
    {
        var close = point.LegalClosePricePercent ?? point.ClosePricePercent;
        if (close is not > 0 || !double.IsFinite(close.Value) || !double.IsFinite(point.FaceValue) || point.FaceValue <= 0 ||
            !double.IsFinite(point.CouponPercent) || point.CouponPercent < 0)
        {
            throw new PortfolioBenchmarkHistoryUnavailableException($"Для {IndexedBondIsin} отсутствует корректная цена, номинал или купон за {point.Date:yyyy-MM-dd}.");
        }

        return close.Value;
    }

    private static double GetAccruedInterest(PortfolioBondBenchmarkPricePoint point)
    {
        if (point.AccruedInterest is null || !double.IsFinite(point.AccruedInterest.Value) || point.AccruedInterest.Value < 0)
        {
            throw new PortfolioBenchmarkHistoryUnavailableException($"Для {IndexedBondIsin} отсутствует фактический НКД за торговую дату {point.Date:yyyy-MM-dd}.");
        }

        return point.AccruedInterest.Value;
    }

    private static DateOnly FindIndexedBondComparisonStartDate(
        DateOnly from,
        DateOnly to,
        PortfolioBondBenchmarkHistory history,
        IReadOnlyCollection<PortfolioValuationInput> valuations)
    {
        if (history.Points.Any(point => point.Date <= from))
        {
            return from;
        }

        var firstPrice = history.Points.Where(point => point.Date > from && point.Date <= to).MinBy(point => point.Date)
            ?? throw new PortfolioBenchmarkHistoryUnavailableException($"Нет цены {IndexedBondIsin} в выбранном периоде.");
        return valuations.Where(value => value.Date >= firstPrice.Date && value.Date < to).MinBy(value => value.Date)?.Date
            ?? throw new PortfolioBenchmarkHistoryUnavailableException($"Нет оценки портфеля на дату {firstPrice.Date:yyyy-MM-dd} или позже до конца периода.");
    }

    private static bool IsRubleUnit(string? unit) => string.Equals(unit, "RUB", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(unit, "SUR", StringComparison.OrdinalIgnoreCase);

    private sealed record PriceValue(DateOnly Date, double Value, bool IsTradingDate);

    private sealed record AssetSimulation(
        DateOnly StartPriceDate,
        DateOnly EndPriceDate,
        IReadOnlyList<PriceValue> Prices,
        IReadOnlyList<PortfolioValuationInput> Valuations);
}
