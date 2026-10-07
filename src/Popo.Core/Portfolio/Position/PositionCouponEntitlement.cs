namespace Popo.Core.Portfolio.Position;

public enum PositionCouponEntitlement
{
    NotEntitled,
    Entitled,
    Ambiguous
}

public interface IPositionCouponEntitlementPolicy
{
    PositionCouponEntitlement Evaluate(OpenPositionLot lot, PositionCouponPayment coupon);
}

public sealed class ZoPositionCouponEntitlementPolicy : IPositionCouponEntitlementPolicy
{
    public PositionCouponEntitlement Evaluate(OpenPositionLot lot, PositionCouponPayment coupon)
    {
        if (lot.TradeDate >= coupon.Date)
            return PositionCouponEntitlement.NotEntitled;

        if (coupon.RecordDate.HasValue)
            return lot.TradeDate < coupon.RecordDate.Value
                ? PositionCouponEntitlement.Entitled
                : PositionCouponEntitlement.NotEntitled;

        return lot.TradeDate < coupon.PeriodStartDate
            ? PositionCouponEntitlement.Entitled
            : PositionCouponEntitlement.Ambiguous;
    }
}

public sealed class FloaterPositionCouponEntitlementPolicy : IPositionCouponEntitlementPolicy
{
    private const int RecordDateSettlementUncertaintyDays = 1;

    public PositionCouponEntitlement Evaluate(OpenPositionLot lot, PositionCouponPayment coupon)
    {
        if (lot.TradeDate >= coupon.Date)
            return PositionCouponEntitlement.NotEntitled;

        if (coupon.RecordDate.HasValue)
        {
            var daysBeforeRecordDate = coupon.RecordDate.Value.DayNumber - lot.TradeDate.DayNumber;
            if (daysBeforeRecordDate is >= 0 and <= RecordDateSettlementUncertaintyDays)
                return PositionCouponEntitlement.Ambiguous;

            return lot.TradeDate < coupon.RecordDate.Value
                ? PositionCouponEntitlement.Entitled
                : PositionCouponEntitlement.NotEntitled;
        }

        return lot.TradeDate < coupon.PeriodStartDate
            ? PositionCouponEntitlement.Entitled
            : PositionCouponEntitlement.Ambiguous;
    }
}
