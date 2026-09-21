namespace Popo.Core.Recommendations;

public sealed record BondYieldCalculationDetails(
    DateOnly SettlementDate,
    double MarketPricePercent,
    double CleanPrice,
    double AccruedInterest,
    double DirtyPrice,
    double FaceValue,
    IReadOnlyList<BondYieldCashFlow> CashFlows);

public record struct BondCandidate(
    string SecId,
    string BoardId,
    int IssuerId,
    string IssuerName,
    string CurrencyId,
    double Ytm,
    double BuyPrice,
    CreditRating Rating,
    DateOnly MaturityDate,
    DateOnly? OfferDate,
    BondCouponType CouponType,
    double MedianDailyVolume,
    DateOnly YieldDate,
    BondYieldDateType YieldDateType,
    BondYieldCalculationDetails Calculation);
