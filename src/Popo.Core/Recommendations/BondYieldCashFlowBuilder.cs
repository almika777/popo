namespace Popo.Core.Recommendations;

public static class BondYieldCashFlowBuilder
{
    private const double DaysInYear = 365d;
    private const double AmountTolerance = 1e-8;

    public static BondYieldSchedule? TryBuild(
        BondYieldRequest request,
        IReadOnlyCollection<BondCouponSource> coupons,
        IReadOnlyCollection<BondPrincipalSource> principalPayments)
    {
        if (!IsValidRequest(request))
            return null;

        var events = new List<CashFlowEvent>();
        var remainingFace = request.SettlementFaceValue;

        foreach (var principal in principalPayments
                     .Where(x => x.Date > request.SettlementDate && x.Date <= request.YieldDate)
                     .OrderBy(x => x.Date))
        {
            var amount = ResolvePrincipalAmount(principal);
            if (amount is null || amount.Value > remainingFace + AmountTolerance)
                return null;

            var appliedAmount = Math.Min(amount.Value, remainingFace);
            if (appliedAmount > AmountTolerance)
            {
                events.Add(new CashFlowEvent(
                    new CashFlow(appliedAmount, principal.Date),
                    BondYieldCashFlowType.Amortization,
                    false));
                remainingFace -= appliedAmount;
            }
        }

        if (remainingFace < -AmountTolerance)
            return null;

        remainingFace = Math.Max(0d, remainingFace);
        if (request.YieldDateType == BondYieldDateType.Maturity &&
            request.RequiresExplicitRedemption &&
            remainingFace > AmountTolerance)
        {
            return null;
        }

        var orderedCoupons = coupons.OrderBy(x => x.Date).ToArray();
        if (request.RequiresCouponSchedule && orderedCoupons.Length == 0)
            return null;

        double? lastKnownAnnualRate = null;
        double? lastKnownCouponValue = null;
        double? lastKnownCouponFaceValue = null;
        var hasRelevantFutureCoupon = false;
        BondCouponSource? offerPeriodCoupon = null;
        double? offerPeriodAnnualRate = null;

        foreach (var coupon in orderedCoupons)
        {
            if (coupon.PeriodStartDate >= coupon.Date)
                return null;

            var coversOfferDate = request.YieldDateType == BondYieldDateType.Offer
                                  && coupon.PeriodStartDate < request.YieldDate
                                  && coupon.Date > request.YieldDate;
            var isRelevant = coupon.Date <= request.YieldDate || coversOfferDate;
            if (!isRelevant)
                continue;

            var faceAtCoupon = coupon.Date <= request.SettlementDate
                ? coupon.FaceValue
                : FaceBeforeDate(request, principalPayments, coupon.Date);
            if (!double.IsFinite(faceAtCoupon) || faceAtCoupon <= 0d)
                return null;

            var periodFraction = (coupon.Date.DayNumber - coupon.PeriodStartDate.DayNumber) / DaysInYear;
            var annualRate = ResolveAnnualRate(coupon, faceAtCoupon, periodFraction, lastKnownAnnualRate);
            if (annualRate is null)
            {
                if (coupon.Date > request.SettlementDate)
                    return null;

                continue;
            }

            lastKnownAnnualRate = annualRate;
            if (coupon.Value is { } knownPublishedValue)
            {
                lastKnownCouponValue = knownPublishedValue;
                lastKnownCouponFaceValue = faceAtCoupon;
            }

            if (coversOfferDate)
            {
                offerPeriodCoupon = coupon;
                offerPeriodAnnualRate = annualRate;
                continue;
            }

            if (coupon.Date <= request.SettlementDate)
                continue;

            hasRelevantFutureCoupon = true;
            var amount = coupon.Value is { } publishedValue
                ? publishedValue
                : lastKnownCouponValue is { } knownCouponValue && lastKnownCouponFaceValue is { } knownCouponFaceValue
                    ? knownCouponValue * faceAtCoupon / knownCouponFaceValue
                    : faceAtCoupon * annualRate.Value * periodFraction;
            if (!double.IsFinite(amount) || amount < 0d)
                return null;

            if (amount > AmountTolerance)
            {
                events.Add(new CashFlowEvent(
                    new CashFlow(amount, coupon.Date),
                    BondYieldCashFlowType.Coupon,
                    coupon.Value is null && coupon.AnnualRatePercent is null));
            }
        }

        if (request.RequiresCouponSchedule &&
            !hasRelevantFutureCoupon &&
            offerPeriodCoupon is null)
        {
            return null;
        }

        if (request.YieldDateType == BondYieldDateType.Offer &&
            !orderedCoupons.Any(x => x.Date == request.YieldDate))
        {
            if (request.RequiresCouponSchedule &&
                (offerPeriodCoupon is null || offerPeriodAnnualRate is null))
            {
                return null;
            }

            if (offerPeriodCoupon is not null && offerPeriodAnnualRate is not null)
            {
                var accruedDays = request.YieldDate.DayNumber - offerPeriodCoupon.PeriodStartDate.DayNumber;
                var accruedCoupon = remainingFace * offerPeriodAnnualRate.Value * accruedDays / DaysInYear;
                if (!double.IsFinite(accruedCoupon) || accruedCoupon < 0d)
                    return null;

                if (accruedCoupon > AmountTolerance)
                {
                    events.Add(new CashFlowEvent(
                        new CashFlow(accruedCoupon, request.YieldDate),
                        BondYieldCashFlowType.Coupon,
                        offerPeriodCoupon.Value is null && offerPeriodCoupon.AnnualRatePercent is null));
                }
            }
        }

        var redemption = request.YieldDateType == BondYieldDateType.Offer
            ? remainingFace * request.OfferPricePercent!.Value / 100d
            : remainingFace;
        if (!double.IsFinite(redemption) || redemption < 0d)
            return null;

        if (redemption > AmountTolerance)
        {
            events.Add(new CashFlowEvent(
                new CashFlow(redemption, request.YieldDate),
                BondYieldCashFlowType.Redemption,
                false));
        }

        var cashFlows = events
            .OrderBy(x => x.Flow.Date)
            .ThenBy(x => x.Type)
            .Select(x => x.Flow)
            .ToArray();
        if (cashFlows.Length == 0)
            return null;

        var cashFlowDetails = events
            .OrderBy(x => x.Flow.Date)
            .ThenBy(x => x.Type)
            .Select(x => new BondYieldCashFlow(x.Flow.Date, x.Flow.ValueInRub, x.Type, x.IsProjected))
            .ToArray();

        return new BondYieldSchedule(
            request.YieldDate,
            request.YieldDateType,
            request.SettlementFaceValue,
            cashFlows,
            cashFlowDetails);
    }

    private static bool IsValidRequest(BondYieldRequest request) =>
        !string.IsNullOrWhiteSpace(request.SecId)
        && !string.IsNullOrWhiteSpace(request.BoardId)
        && !string.IsNullOrWhiteSpace(request.Isin)
        && request.YieldDate > request.SettlementDate
        && double.IsFinite(request.SettlementFaceValue)
        && request.SettlementFaceValue > 0d
        && !string.IsNullOrWhiteSpace(request.FaceUnit)
        && !string.IsNullOrWhiteSpace(request.SettlementCurrency)
        && (request.YieldDateType != BondYieldDateType.Offer
            || request.OfferPricePercent is { } offerPrice
            && double.IsFinite(offerPrice)
            && offerPrice > 0d);

    private static double? ResolvePrincipalAmount(BondPrincipalSource principal)
    {
        if (principal.Value is { } value)
            return double.IsFinite(value) && value >= 0d ? value : null;

        if (principal.ValuePercent is not { } percent ||
            !double.IsFinite(percent) || percent < 0d ||
            !double.IsFinite(principal.InitialFaceValue) || principal.InitialFaceValue <= 0d)
        {
            return null;
        }

        var amount = principal.InitialFaceValue * percent / 100d;
        return double.IsFinite(amount) && amount >= 0d ? amount : null;
    }

    private static double FaceBeforeDate(
        BondYieldRequest request,
        IReadOnlyCollection<BondPrincipalSource> principalPayments,
        DateOnly date)
    {
        var face = request.SettlementFaceValue;
        foreach (var principal in principalPayments
                     .Where(x => x.Date > request.SettlementDate && x.Date < date)
                     .OrderBy(x => x.Date))
        {
            var amount = ResolvePrincipalAmount(principal);
            if (amount is null)
                return double.NaN;

            face -= amount.Value;
        }

        return face;
    }

    private static double? ResolveAnnualRate(
        BondCouponSource coupon,
        double faceAtCoupon,
        double periodFraction,
        double? lastKnownAnnualRate)
    {
        if (!double.IsFinite(periodFraction) || periodFraction <= 0d)
            return null;

        if (coupon.Value is { } value)
        {
            if (!double.IsFinite(value) || value < 0d)
                return null;

            var annualRate = value / faceAtCoupon / periodFraction;
            return double.IsFinite(annualRate) && annualRate >= 0d ? annualRate : null;
        }

        if (coupon.AnnualRatePercent is { } ratePercent)
        {
            var annualRate = ratePercent / 100d;
            return double.IsFinite(annualRate) && annualRate >= 0d ? annualRate : null;
        }

        return lastKnownAnnualRate;
    }

    private readonly record struct CashFlowEvent(
        CashFlow Flow,
        BondYieldCashFlowType Type,
        bool IsProjected);
}
