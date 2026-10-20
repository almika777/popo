namespace Popo.Core.Portfolio.ClosedSessions;

public sealed record ClosedSessionTrade(
    Guid Id, DateOnly Date, TradeSide Side, double Quantity, double Price, double Commission);

public sealed record ClosedSessionCoupon(
    DateOnly StartDate, DateOnly EndDate, double? Value, double? ValueInRub, string FaceUnit);

public sealed record ClosedSessionAmortization(
    DateOnly Date, double? Value, double? ValueInRub, double? ValuePercent,
    double InitialFaceValue, string FaceUnit);

public sealed record ClosedSessionCurrencyRate(DateOnly Date, string Currency, double UnitRate);

public sealed record ClosedSessionInstrument(
    string SecId, 
    string BoardId, 
    string CurrencyId, 
    string ShortName, 
    string FaceUnit,
    string BondType, 
    string BondSubType, 
    int? CouponFrequency, 
    double InitialFaceValue,
    DateOnly? MaturityDate, 
    IReadOnlyList<ClosedSessionTrade> Trades,
    IReadOnlyList<ClosedSessionCoupon> Coupons, 
    IReadOnlyList<ClosedSessionAmortization> Amortizations,
    IReadOnlyList<ClosedSessionCurrencyRate> CurrencyRates);

/// <summary>Остаток действует с From включительно до To исключительно.</summary>
public sealed record ClosedSessionHolding(DateOnly From, DateOnly To, double Quantity);

public sealed record ClosedPositionSession(
    Guid Id, string SecId, string BoardId, string CurrencyId, string ShortName,
    DateOnly StartDate, DateOnly EndDate, int HoldingDays, double BoughtQuantity,
    double BoughtCleanAmount, double SoldCleanAmount, double Commission,
    double? AmortizationIncome, double? BodyPnl, double? CouponIncome, double? TotalPnl,
    double? TotalPnlPercent, double? AnnualizedPnlPercent, bool IsComplete,
    string? IncompleteReason, string CalculatorKind, DateTimeOffset CalculatedAt);

public sealed record ClosedSessionIncome(double? Amount, string? IncompleteReason = null);
