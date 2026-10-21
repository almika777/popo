namespace Popo.Core.Portfolio.Position;

public sealed class FloaterPositionCalculator : IPositionCalculator
{
    private static readonly PositionCouponHistoryCalculator CouponHistoryCalculator = new();
    private static readonly IPositionCouponEntitlementPolicy CouponEntitlementPolicy =
        new FloaterPositionCouponEntitlementPolicy();

    public PositionCalculationResult Calculate(PositionCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var couponValuationDate = input.CouponValuationDate ?? input.AsOf;
        var openLots = PositionOpenLotCalculator.Calculate(input.Trades, input.AsOf);
        if (openLots.RemainingQuantity <= 0)
        {
            return new PositionCalculationResult(
                null,
                PortfolioPositionCalculator.Calculate(input.Position, input.CurrentCleanPrice),
                null);
        }

        var remainingQuantity = openLots.RemainingQuantity;
        var averageBuyPrice = openLots.RemainingCleanCost / remainingQuantity;
        var valuation = PortfolioPositionCalculator.Calculate(
            input.Position with { Quantity = remainingQuantity },
            input.CurrentCleanPrice,
            averageBuyPrice);
        if (!IsPositiveFinite(input.CurrentCleanPrice))
            return new PositionCalculationResult(null, valuation, averageBuyPrice, averageBuyPrice);

        if (!input.CurrentAccruedInterest.HasValue
            || !double.IsFinite(input.CurrentAccruedInterest.Value)
            || input.CurrentAccruedInterest.Value < 0)
            return new PositionCalculationResult(null, valuation, averageBuyPrice, averageBuyPrice);

        var couponScheduleComplete = openLots.Lots.All(x => x.TradeDate <= couponValuationDate)
                                     && HasCompleteCouponSchedule(
            openLots.Lots,
            input.CouponPayments,
            couponValuationDate,
            input.NextCouponDate,
            input.CouponFrequency);
        var couponResult = CouponHistoryCalculator.Calculate(
            openLots.Lots,
            input.CouponPayments,
            couponValuationDate,
            CouponEntitlementPolicy,
            GetCouponAmountInRub,
            couponScheduleComplete);

        var paidBuyAccruedInterest = openLots.PaidBuyAccruedInterest;
        var currentAccruedInterest = remainingQuantity * input.CurrentAccruedInterest.Value;
        var couponIncome = couponResult.HistoricalIncome + currentAccruedInterest - paidBuyAccruedInterest;
        var cleanPnl = valuation.UnrealizedPnl;
        var totalPnl = cleanPnl.GetValueOrDefault() + couponIncome - openLots.PaidBuyCommission;
        var totalPnlPercent = openLots.RemainingCleanCost <= 0
            ? 0
            : totalPnl / openLots.RemainingCleanCost * 100;
        var annualizedPnlPercent = couponResult.IsComplete && valuation.MarketValue.HasValue
            ? CalculateAnnualizedPnlPercent(
                couponResult.PerLotIncome,
                couponValuationDate,
                input.CurrentCleanPrice!.Value,
                input.CurrentAccruedInterest.Value,
                openLots.RemainingCleanCost)
            : null;

        var income = new PortfolioPositionIncome(
            openLots.RemainingCleanCost,
            openLots.PaidBuyCommission,
            couponIncome,
            totalPnl,
            totalPnlPercent,
            remainingQuantity,
            openLots.AverageBuyPricePercent,
            cleanPnl,
            valuation.UnrealizedPnlPercent,
            openLots.ActualRemainingCleanCost,
            annualizedPnlPercent,
            couponResult.HistoricalIncome,
            paidBuyAccruedInterest,
            currentAccruedInterest,
            couponResult.IsComplete);

        return new PositionCalculationResult(income, valuation, averageBuyPrice, averageBuyPrice);
    }

    private static bool HasCompleteCouponSchedule(
        IReadOnlyCollection<OpenPositionLot> openLots,
        IReadOnlyCollection<PositionCouponPayment> couponSchedule,
        DateOnly asOf,
        DateOnly? nextCouponDate,
        int? couponFrequency)
    {
        var earliestOpenTradeDate = openLots.Min(x => x.TradeDate);
        var relevantSchedule = couponSchedule
            .Where(x => x.Date > earliestOpenTradeDate)
            .ToArray();

        if (couponFrequency == 0)
            return relevantSchedule.Length == 0;

        if (relevantSchedule.Length == 0)
            return false;

        var orderedSchedule = relevantSchedule.OrderBy(x => x.Date).ToArray();
        if (orderedSchedule.Any(x => x.PeriodStartDate >= x.Date
                                     || x.RecordDate.HasValue && x.RecordDate.Value > x.Date))
            return false;

        if (orderedSchedule.GroupBy(x => x.Date).Any(group => group.Distinct().Count() > 1))
            return false;

        const int scheduleDateToleranceDays = 7;
        foreach (var lot in openLots)
        {
            var firstCouponAfterPurchase = orderedSchedule.FirstOrDefault(x => x.Date > lot.TradeDate);
            if (firstCouponAfterPurchase is null
                || firstCouponAfterPurchase.PeriodStartDate.DayNumber - lot.TradeDate.DayNumber
                > scheduleDateToleranceDays)
                return false;
        }

        for (var index = 1; index < orderedSchedule.Length; index++)
        {
            var previousDate = orderedSchedule[index - 1].Date;
            var currentPeriodStart = orderedSchedule[index].PeriodStartDate;
            if (currentPeriodStart.DayNumber - previousDate.DayNumber > scheduleDateToleranceDays)
                return false;
        }

        if (nextCouponDate.HasValue
            && !orderedSchedule.Any(x => Math.Abs(x.Date.DayNumber - nextCouponDate.Value.DayNumber)
                                         <= scheduleDateToleranceDays))
            return false;

        return orderedSchedule.Any(x => x.Date > asOf)
               || couponFrequency == 0;
    }

    private static double? CalculateAnnualizedPnlPercent(
        IReadOnlyList<OpenPositionLotCouponIncome> perLotIncome,
        DateOnly asOf,
        double currentCleanPrice,
        double currentAccruedInterest,
        double remainingCleanCost)
    {
        if (remainingCleanCost <= 0)
            return null;

        var annualizedAmount = perLotIncome.Sum(lotIncome =>
        {
            var lot = lotIncome.Lot;
            var holdingDays = Math.Max(1, asOf.DayNumber - lot.TradeDate.DayNumber);
            var totalPnl = lot.Quantity * (currentCleanPrice - lot.CleanPrice)
                           + lotIncome.Amount
                           + lot.Quantity * currentAccruedInterest
                           - lot.Quantity * lot.PaidAccruedInterestPerUnit
                           - lot.Quantity * lot.CommissionPerUnit;
            return totalPnl * 365 / holdingDays;
        });

        return annualizedAmount / remainingCleanCost * 100;
    }

    private static double? GetCouponAmountInRub(PositionCouponPayment coupon)
    {
        if (coupon.ValueInRub.HasValue && double.IsFinite(coupon.ValueInRub.Value)
            && coupon.ValueInRub.Value >= 0)
            return coupon.ValueInRub.Value;

        if (coupon.Value.HasValue && double.IsFinite(coupon.Value.Value) && coupon.Value.Value >= 0
            && (string.Equals(coupon.FaceUnit, "RUB", StringComparison.OrdinalIgnoreCase)
                || string.Equals(coupon.FaceUnit, "SUR", StringComparison.OrdinalIgnoreCase)))
            return coupon.Value.Value;

        return null;
    }

    private static bool IsPositiveFinite(double? value) =>
        value.HasValue && double.IsFinite(value.Value) && value.Value > 0;
}
