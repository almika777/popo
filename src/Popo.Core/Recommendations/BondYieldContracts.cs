namespace Popo.Core.Recommendations;

public enum BondYieldDateType
{
    Maturity,
    Offer
}

public enum BondYieldCashFlowType
{
    Coupon,
    Amortization,
    Redemption
}

public sealed record BondYieldCashFlow(
    DateOnly Date,
    double Amount,
    BondYieldCashFlowType Type,
    bool IsProjected);

public sealed record BondYieldRequest(
    string SecId,
    string BoardId,
    string Isin,
    DateOnly SettlementDate,
    DateOnly YieldDate,
    BondYieldDateType YieldDateType,
    double SettlementFaceValue,
    string FaceUnit,
    string SettlementCurrency,
    double? OfferPricePercent,
    bool RequiresCouponSchedule,
    bool RequiresExplicitRedemption = false);

public sealed record BondCouponSource(
    DateOnly Date,
    DateOnly PeriodStartDate,
    double? Value,
    double FaceValue,
    double? AnnualRatePercent);

public sealed record BondPrincipalSource(
    DateOnly Date,
    double? Value,
    double? ValuePercent,
    double InitialFaceValue);

public sealed record BondYieldSchedule(
    DateOnly YieldDate,
    BondYieldDateType YieldDateType,
    double FaceValueInSettlementCurrency,
    IReadOnlyList<CashFlow> CashFlows,
    IReadOnlyList<BondYieldCashFlow> CashFlowDetails);
