namespace Popo.Core.Portfolio.Position;

public sealed record ZoCouponPayment(
    DateOnly Date,
    DateOnly PeriodStartDate,
    double? Value,
    string FaceUnit,
    double? RateToRub,
    DateOnly? RecordDate = null,
    double? ValueInRub = null);

public sealed record PositionCalculationInput(
    PortfolioPositionRecord Position,
    IReadOnlyCollection<PositionIncomeTrade> Trades,
    DateOnly AsOf,
    double? CurrentCleanPrice,
    double CouponValue,
    int CouponPeriodDays,
    bool IncludeIndexedNominalIncreaseInPnl,
    double CurrentAccruedInterest,
    IReadOnlyCollection<ZoCouponPayment> CouponPayments,
    int? CouponFrequency = null,
    DateOnly? NextCouponDate = null);

public sealed record PositionCalculationResult(
    PortfolioPositionIncome? Income,
    PortfolioPositionValuation Valuation,
    double? AverageBuyPriceAtCurrentFaceValue,
    double? AverageBuyPrice = null,
    bool IsTradeOrderAmbiguous = false);

public interface IPositionCalculator
{
    PositionCalculationResult Calculate(PositionCalculationInput input);
}
