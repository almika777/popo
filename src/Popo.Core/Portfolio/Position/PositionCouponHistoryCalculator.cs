namespace Popo.Core.Portfolio.Position;

public sealed class PositionCouponHistoryCalculator
{
    public PositionCouponHistoryResult Calculate(
        IReadOnlyCollection<OpenPositionLot> lots,
        IReadOnlyCollection<PositionCouponPayment> couponSchedule,
        DateOnly asOf,
        IPositionCouponEntitlementPolicy entitlementPolicy,
        Func<PositionCouponPayment, double?> getPaymentAmount,
        bool isScheduleComplete)
    {
        ArgumentNullException.ThrowIfNull(lots);
        ArgumentNullException.ThrowIfNull(couponSchedule);
        ArgumentNullException.ThrowIfNull(entitlementPolicy);
        ArgumentNullException.ThrowIfNull(getPaymentAmount);

        var isComplete = isScheduleComplete;
        var knownPayments = new List<PositionCouponPayment>();
        var earliestOpenTradeDate = lots.Count == 0 ? asOf : lots.Min(x => x.TradeDate);
        foreach (var paymentsOnDate in couponSchedule
                     .Where(x => x.Date > earliestOpenTradeDate)
                     .GroupBy(x => x.Date))
        {
            var distinctPayments = paymentsOnDate.Distinct().ToArray();
            if (distinctPayments.Length != 1)
            {
                isComplete = false;
                continue;
            }

            if (paymentsOnDate.Key <= asOf)
                knownPayments.Add(distinctPayments[0]);
        }

        var lotResults = new List<OpenPositionLotCouponIncome>(lots.Count);
        foreach (var lot in lots)
        {
            var income = 0d;
            foreach (var coupon in knownPayments)
            {
                if (coupon.PeriodStartDate >= coupon.Date)
                {
                    if (lot.TradeDate < coupon.Date)
                        isComplete = false;
                    continue;
                }

                var entitlement = entitlementPolicy.Evaluate(lot, coupon);
                if (entitlement == PositionCouponEntitlement.Ambiguous)
                {
                    isComplete = false;
                    continue;
                }

                if (entitlement == PositionCouponEntitlement.NotEntitled)
                    continue;

                var amount = getPaymentAmount(coupon);
                if (!IsValidAmount(amount))
                {
                    isComplete = false;
                    continue;
                }

                income += amount!.Value * lot.Quantity;
            }

            lotResults.Add(new OpenPositionLotCouponIncome(lot, income));
        }

        return new PositionCouponHistoryResult(lotResults, isComplete);
    }

    private static bool IsValidAmount(double? amount) =>
        amount.HasValue && double.IsFinite(amount.Value) && amount.Value >= 0;
}

public sealed record OpenPositionLotCouponIncome(OpenPositionLot Lot, double Amount);

public sealed class PositionCouponHistoryResult(
    IReadOnlyList<OpenPositionLotCouponIncome> perLotIncome,
    bool isComplete)
{
    public IReadOnlyList<OpenPositionLotCouponIncome> PerLotIncome { get; } = perLotIncome;
    public double HistoricalIncome => PerLotIncome.Sum(x => x.Amount);
    public bool IsComplete { get; } = isComplete;
}
