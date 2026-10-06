namespace Popo.Core.Portfolio.Position;

public sealed class ZoPositionCalculator : IPositionCalculator
{
    public PositionCalculationResult Calculate(PositionCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var lots = BuildOpenLots(input.Trades, input.AsOf);
        var remainingQuantity = lots.Sum(x => x.Quantity);
        if (remainingQuantity <= 0)
        {
            return new PositionCalculationResult(
                null,
                PortfolioPositionCalculator.Calculate(input.Position, input.CurrentCleanPrice),
                null);
        }

        var remainingCleanCost = lots.Sum(x => x.Quantity * x.CleanPrice);
        var averageBuyPrice = remainingCleanCost / remainingQuantity;
        var hasAmbiguousTradeOrder = HasAmbiguousTradeOrder(input.Trades);
        if (hasAmbiguousTradeOrder)
        {
            var ambiguousMarketPrice = IsPositiveFinite(input.CurrentCleanPrice)
                ? input.CurrentCleanPrice
                : null;
            var ambiguousMarketValue = IsPositiveFinite(input.CurrentCleanPrice)
                ? remainingQuantity * input.CurrentCleanPrice!.Value
                : (double?)null;
            return new PositionCalculationResult(
                null,
                new PortfolioPositionValuation(ambiguousMarketPrice, ambiguousMarketValue, null, null),
                null,
                null,
                true);
        }

        if (!IsPositiveFinite(input.CurrentCleanPrice))
        {
            var valuationWithoutQuote = PortfolioPositionCalculator.Calculate(
                input.Position with { Quantity = remainingQuantity },
                input.CurrentCleanPrice,
                averageBuyPrice);
            return new PositionCalculationResult(
                null,
                valuationWithoutQuote,
                averageBuyPrice,
                averageBuyPrice);
        }

        if (!double.IsFinite(input.CurrentAccruedInterest))
            throw new ArgumentException("Текущий НКД для расчёта результата позиции некорректен.");

        var paidBuyCommission = lots.Sum(x => x.Quantity * x.CommissionPerUnit);
        var paidBuyAccruedInterest = lots.Sum(x => x.Quantity * x.PaidAccruedInterestPerUnit);
        var currentAccruedInterest = remainingQuantity * input.CurrentAccruedInterest;
        var couponResult = CalculateHistoricalCoupons(
            lots,
            input.CouponPayments,
            input.AsOf,
            input.CouponPeriodDays,
            input.CouponValue,
            input.CurrentAccruedInterest,
            input.CouponFrequency,
            input.NextCouponDate);
        var couponIncome = couponResult.Income + currentAccruedInterest - paidBuyAccruedInterest;
        var marketPrice = input.CurrentCleanPrice;
        var marketValue = IsPositiveFinite(marketPrice) ? remainingQuantity * marketPrice!.Value : (double?)null;
        var unrealizedPnl = marketValue.HasValue ? marketValue.Value - remainingCleanCost : (double?)null;
        var unrealizedPnlPercent = unrealizedPnl.HasValue && remainingCleanCost > 0
            ? unrealizedPnl.Value / remainingCleanCost * 100
            : (double?)null;
        var totalPnl = unrealizedPnl.GetValueOrDefault() + couponIncome - paidBuyCommission;
        var totalPnlPercent = remainingCleanCost <= 0 ? 0 : totalPnl / remainingCleanCost * 100;
        var annualizedTotalPnlPercent = couponResult.IsComplete && marketValue.HasValue
            ? CalculateAnnualizedResult(lots, input, remainingCleanCost)
            : null;
        var income = new PortfolioPositionIncome(
            remainingCleanCost,
            paidBuyCommission,
            couponIncome,
            totalPnl,
            totalPnlPercent,
            remainingQuantity,
            CalculateAveragePricePercent(lots),
            unrealizedPnl,
            unrealizedPnlPercent,
            remainingCleanCost,
            annualizedTotalPnlPercent,
            couponResult.Income,
            paidBuyAccruedInterest,
            currentAccruedInterest,
            couponResult.IsComplete);
        var valuation = PortfolioPositionCalculator.Calculate(
            input.Position with { Quantity = remainingQuantity },
            marketPrice,
            remainingCleanCost / remainingQuantity);

        return new PositionCalculationResult(
            income,
            valuation,
            averageBuyPrice,
            averageBuyPrice);
    }

    private static IReadOnlyList<OpenLot> BuildOpenLots(
        IReadOnlyCollection<PositionIncomeTrade> trades,
        DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(trades);
        var lots = new Queue<OpenLot>();

        foreach (var trade in PortfolioPositionsValidate.OrderByExecution(
                     trades,
                     x => x.TradeDate,
                     x => x.Side,
                     x => x.TradeTime))
        {
            PortfolioPositionsValidate.ValidateTrade(trade, asOf);
            if (!double.IsFinite(trade.AccruedInterest) || trade.AccruedInterest < 0)
                throw new ArgumentException("НКД сделки для расчёта результата позиции некорректен.");

            if (trade.Side == TradeSide.Buy)
            {
                lots.Enqueue(new OpenLot(
                    trade.TradeDate,
                    trade.Quantity,
                    trade.ActualCleanPrice ?? trade.CleanPrice,
                    trade.Commission / trade.Quantity,
                    trade.AccruedInterest,
                    trade.CleanPricePercent));
                continue;
            }

            var quantityToSell = trade.Quantity;
            while (quantityToSell > 0 && lots.TryPeek(out var lot))
            {
                var consumedQuantity = Math.Min(quantityToSell, lot.Quantity);
                lot.Quantity -= consumedQuantity;
                quantityToSell -= consumedQuantity;
                if (lot.Quantity == 0)
                    lots.Dequeue();
            }

            if (quantityToSell > 0)
                throw new InvalidOperationException("Количество продажи не может превышать доступный остаток.");
        }

        return lots.ToArray();
    }

    private static CouponCalculation CalculateHistoricalCoupons(
        IReadOnlyCollection<OpenLot> lots,
        IReadOnlyCollection<ZoCouponPayment> couponPayments,
        DateOnly asOf,
        int couponPeriodDays,
        double couponValue,
        double currentAccruedInterest,
        int? couponFrequency,
        DateOnly? nextCouponDate)
    {
        ArgumentNullException.ThrowIfNull(couponPayments);
        var income = 0d;
        var history = couponPayments
            .Where(x => x.Date <= asOf)
            .OrderBy(x => x.Date)
            .ToArray();
        var isComplete = HasCompleteCouponHistory(
            lots,
            history,
            asOf,
            couponPeriodDays,
            couponValue,
            currentAccruedInterest,
            couponFrequency,
            nextCouponDate);

        foreach (var coupon in history)
        {
            if (coupon.PeriodStartDate >= coupon.Date)
            {
                if (lots.Any(lot => lot.TradeDate < coupon.Date))
                    isComplete = false;
                continue;
            }

            var entitledQuantity = 0d;
            var ambiguousEntitlement = false;
            foreach (var lot in lots.Where(lot => lot.TradeDate < coupon.Date))
            {
                if (coupon.RecordDate.HasValue
                    ? lot.TradeDate < coupon.RecordDate.Value
                    : lot.TradeDate < coupon.PeriodStartDate)
                    entitledQuantity += lot.Quantity;
                else if (!coupon.RecordDate.HasValue)
                    ambiguousEntitlement = true;
            }

            if (ambiguousEntitlement)
                isComplete = false;

            if (entitledQuantity == 0)
                continue;

            var couponAmountRub = GetCouponAmountInRub(coupon);
            if (!couponAmountRub.HasValue)
            {
                isComplete = false;
                continue;
            }

            income += entitledQuantity * couponAmountRub.Value;
        }

        return new CouponCalculation(income, isComplete);
    }

    private static bool HasCompleteCouponHistory(
        IReadOnlyCollection<OpenLot> lots,
        IReadOnlyList<ZoCouponPayment> couponPayments,
        DateOnly asOf,
        int couponPeriodDays,
        double couponValue,
        double currentAccruedInterest,
        int? couponFrequency,
        DateOnly? nextCouponDate)
    {
        const int scheduleDateToleranceDays = 7;
        var hasCouponActivity = couponPayments.Count > 0
                                || couponValue > 0
                                || currentAccruedInterest > 0
                                || couponPeriodDays > 0
                                || couponFrequency > 0;

        if (couponFrequency == 0)
            return !hasCouponActivity;

        if (couponFrequency is null && !hasCouponActivity)
            return false;

        if (hasCouponActivity && couponPeriodDays <= 0)
            return false;

        foreach (var lot in lots)
        {
            if (nextCouponDate.HasValue && couponPeriodDays > 0
                && !HasScheduledCouponHistory(lot.TradeDate, couponPayments, asOf, nextCouponDate.Value,
                    couponPeriodDays, scheduleDateToleranceDays))
                return false;

            var previousPayment = couponPayments.LastOrDefault(x => x.Date <= lot.TradeDate);
            var lastRelevantPaymentDate = previousPayment?.Date;

            foreach (var coupon in couponPayments.Where(x => x.Date > lot.TradeDate))
            {
                var scheduleAnchor = lastRelevantPaymentDate ?? lot.TradeDate;
                if (coupon.PeriodStartDate.DayNumber - scheduleAnchor.DayNumber > scheduleDateToleranceDays)
                    return false;

                lastRelevantPaymentDate = coupon.Date;
            }

            var coverageAnchor = lastRelevantPaymentDate ?? lot.TradeDate;
            var daysSinceCoverageAnchor = asOf.DayNumber - coverageAnchor.DayNumber;

            if (couponPeriodDays > 0 && daysSinceCoverageAnchor >= couponPeriodDays)
                return false;

        }

        return true;
    }

    private static bool HasScheduledCouponHistory(
        DateOnly tradeDate,
        IReadOnlyList<ZoCouponPayment> couponPayments,
        DateOnly asOf,
        DateOnly nextCouponDate,
        int couponPeriodDays,
        int scheduleDateToleranceDays)
    {
        var scheduledDate = nextCouponDate;
        while (scheduledDate <= asOf)
            scheduledDate = scheduledDate.AddDays(couponPeriodDays);

        var expectedDate = scheduledDate.AddDays(-couponPeriodDays);
        return expectedDate <= tradeDate
               || expectedDate > asOf
               || couponPayments.Any(coupon => Math.Abs(coupon.Date.DayNumber - expectedDate.DayNumber)
                                                <= scheduleDateToleranceDays);
    }

    private static double? CalculateAnnualizedResult(
        IReadOnlyCollection<OpenLot> lots,
        PositionCalculationInput input,
        double remainingCleanCost)
    {
        if (!input.CurrentCleanPrice.HasValue || remainingCleanCost <= 0)
            return null;

        var annualizedAmount = lots.Sum(lot =>
        {
            var holdingDays = Math.Max(1, input.AsOf.DayNumber - lot.TradeDate.DayNumber);
            var lotCouponIncome = input.CouponPayments
                .Where(x => x.Date > lot.TradeDate && x.Date <= input.AsOf
                            && IsEntitledToCoupon(lot.TradeDate, x))
                .Sum(x => lot.Quantity * GetCouponAmountInRub(x).GetValueOrDefault());
            var totalPnl = lot.Quantity * (input.CurrentCleanPrice.Value - lot.CleanPrice)
                           + lotCouponIncome
                           + lot.Quantity * input.CurrentAccruedInterest
                           - lot.Quantity * lot.PaidAccruedInterestPerUnit
                           - lot.Quantity * lot.CommissionPerUnit;
            return totalPnl * 365 / holdingDays;
        });

        return annualizedAmount / remainingCleanCost * 100;
    }

    private static double? CalculateAveragePricePercent(IReadOnlyCollection<OpenLot> lots) =>
        lots.Count == 0 || lots.Any(x => !x.CleanPricePercent.HasValue)
            ? null
            : lots.Sum(x => x.Quantity * x.CleanPricePercent!.Value) / lots.Sum(x => x.Quantity);

    private static bool HasAmbiguousTradeOrder(IReadOnlyCollection<PositionIncomeTrade> trades)
    {
        var orderedTrades = trades.OrderBy(x => x.TradeDate).ToArray();
        foreach (var sameDay in orderedTrades.GroupBy(x => x.TradeDate))
        {
            var dayTrades = sameDay.ToArray();
            var hasMixedSides = dayTrades.Any(x => x.Side == TradeSide.Buy)
                                && dayTrades.Any(x => x.Side == TradeSide.Sell);
            if (hasMixedSides
                && (dayTrades.Any(x => !x.TradeTime.HasValue)
                    || dayTrades.GroupBy(x => x.TradeTime).Any(x => x.Count() > 1)))
                return true;

            var buys = dayTrades.Where(x => x.Side == TradeSide.Buy).ToArray();
            if (buys.Length < 2
                || buys.All(x => x.TradeTime.HasValue)
                   && buys.Select(x => x.TradeTime).Distinct().Count() == buys.Length
                || !orderedTrades.Any(x => x.Side == TradeSide.Sell && x.TradeDate > sameDay.Key))
                continue;

            if (buys.Select(x => (x.ActualCleanPrice ?? x.CleanPrice, x.Commission / x.Quantity,
                    x.AccruedInterest))
                .Distinct()
                .Skip(1)
                .Any())
                return true;
        }

        return false;
    }

    private static bool IsEntitledToCoupon(DateOnly tradeDate, ZoCouponPayment coupon) =>
        coupon.RecordDate.HasValue
            ? tradeDate < coupon.RecordDate.Value
            : tradeDate < coupon.PeriodStartDate && coupon.PeriodStartDate < coupon.Date;

    private static double? GetCouponAmountInRub(ZoCouponPayment coupon)
    {
        if (coupon.ValueInRub.HasValue && double.IsFinite(coupon.ValueInRub.Value) && coupon.ValueInRub.Value >= 0)
            return coupon.ValueInRub.Value;

        if (coupon.Value.HasValue && double.IsFinite(coupon.Value.Value) && coupon.Value.Value >= 0
            && IsPositiveFinite(coupon.RateToRub))
            return coupon.Value.Value * coupon.RateToRub!.Value;

        return null;
    }

    private static bool IsPositiveFinite(double? value) =>
        value.HasValue && double.IsFinite(value.Value) && value.Value > 0;

    private sealed class OpenLot(
        DateOnly tradeDate,
        double quantity,
        double cleanPrice,
        double commissionPerUnit,
        double paidAccruedInterestPerUnit,
        double? cleanPricePercent)
    {
        public DateOnly TradeDate { get; } = tradeDate;
        public double Quantity { get; set; } = quantity;
        public double CleanPrice { get; } = cleanPrice;
        public double CommissionPerUnit { get; } = commissionPerUnit;
        public double PaidAccruedInterestPerUnit { get; } = paidAccruedInterestPerUnit;
        public double? CleanPricePercent { get; } = cleanPricePercent;
    }

    private sealed record CouponCalculation(double Income, bool IsComplete);
}
