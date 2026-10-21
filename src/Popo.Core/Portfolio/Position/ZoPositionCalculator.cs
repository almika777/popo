namespace Popo.Core.Portfolio.Position;

public sealed class ZoPositionCalculator : IPositionCalculator
{
    public PositionCalculationResult Calculate(PositionCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var openLots = PositionOpenLotCalculator.Calculate(input.Trades, input.AsOf);
        var lots = openLots.Lots;
        var remainingQuantity = openLots.RemainingQuantity;
        if (remainingQuantity <= 0)
        {
            return new PositionCalculationResult(
                null,
                PortfolioPositionCalculator.Calculate(input.Position, input.CurrentCleanPrice),
                null);
        }

        var remainingCleanCost = openLots.RemainingCleanCost;
        var averageBuyPrice = remainingCleanCost / remainingQuantity;
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

        if (!input.CurrentAccruedInterest.HasValue)
        {
            var valuationWithoutAccruedInterest = PortfolioPositionCalculator.Calculate(
                input.Position with { Quantity = remainingQuantity },
                input.CurrentCleanPrice,
                averageBuyPrice);
            return new PositionCalculationResult(
                null,
                valuationWithoutAccruedInterest,
                averageBuyPrice,
                averageBuyPrice);
        }

        if (!double.IsFinite(input.CurrentAccruedInterest.Value) || input.CurrentAccruedInterest.Value < 0)
            throw new ArgumentException("Текущий НКД для расчёта результата позиции некорректен.");

        var paidBuyCommission = openLots.PaidBuyCommission;
        var paidBuyAccruedInterest = openLots.PaidBuyAccruedInterest;
        var currentAccruedInterest = remainingQuantity * input.CurrentAccruedInterest.Value;
        var couponResult = CalculateHistoricalCoupons(
            lots,
            input.CouponPayments,
            input.AsOf,
            input.CouponPeriodDays,
            input.CouponValue,
            input.CurrentAccruedInterest.Value,
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
            ? CalculateAnnualizedResult(couponResult.PerLotIncome, input, remainingCleanCost)
            : null;
        var income = new PortfolioPositionIncome(
            remainingCleanCost,
            paidBuyCommission,
            couponIncome,
            totalPnl,
            totalPnlPercent,
            remainingQuantity,
            openLots.AverageBuyPricePercent,
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

    private static CouponCalculation CalculateHistoricalCoupons(
        IReadOnlyCollection<OpenPositionLot> lots,
        IReadOnlyCollection<PositionCouponPayment> couponPayments,
        DateOnly asOf,
        int couponPeriodDays,
        double couponValue,
        double currentAccruedInterest,
        int? couponFrequency,
        DateOnly? nextCouponDate)
    {
        ArgumentNullException.ThrowIfNull(couponPayments);
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
        var calculation = new PositionCouponHistoryCalculator().Calculate(
            lots,
            history,
            asOf,
            new ZoPositionCouponEntitlementPolicy(),
            GetCouponAmountInRub,
            isComplete);

        return new CouponCalculation(calculation.HistoricalIncome, calculation.IsComplete,
            calculation.PerLotIncome);
    }

    private static bool HasCompleteCouponHistory(
        IReadOnlyCollection<OpenPositionLot> lots,
        IReadOnlyList<PositionCouponPayment> couponPayments,
        DateOnly asOf,
        int couponPeriodDays,
        double couponValue,
        double? currentAccruedInterest,
        int? couponFrequency,
        DateOnly? nextCouponDate)
    {
        const int scheduleDateToleranceDays = 7;
        var hasCouponActivity = couponPayments.Count > 0
                                || couponValue > 0
                                || currentAccruedInterest.GetValueOrDefault() > 0
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
        IReadOnlyList<PositionCouponPayment> couponPayments,
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
        IReadOnlyList<OpenPositionLotCouponIncome> perLotIncome,
        PositionCalculationInput input,
        double remainingCleanCost)
    {
        if (!input.CurrentCleanPrice.HasValue || remainingCleanCost <= 0)
            return null;

        var annualizedAmount = perLotIncome.Sum(lotIncome =>
        {
            var lot = lotIncome.Lot;
            var holdingDays = Math.Max(1, input.AsOf.DayNumber - lot.TradeDate.DayNumber);
            var totalPnl = lot.Quantity * (input.CurrentCleanPrice.Value - lot.CleanPrice)
                           + lotIncome.Amount
                           + lot.Quantity * input.CurrentAccruedInterest!.Value
                           - lot.Quantity * lot.PaidAccruedInterestPerUnit
                           - lot.Quantity * lot.CommissionPerUnit;
            return totalPnl * 365 / holdingDays;
        });

        return annualizedAmount / remainingCleanCost * 100;
    }

    private static double? GetCouponAmountInRub(PositionCouponPayment coupon)
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

    private sealed record CouponCalculation(
        double Income,
        bool IsComplete,
        IReadOnlyList<OpenPositionLotCouponIncome> PerLotIncome);
}
