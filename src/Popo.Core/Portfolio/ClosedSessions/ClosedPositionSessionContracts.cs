namespace Popo.Core.Portfolio.ClosedSessions;

public sealed record ClosedSessionTrade(
    Guid Id, DateOnly Date, TradeSide Side, decimal Quantity, decimal Price, decimal Commission);

public sealed record ClosedSessionCoupon(
    DateOnly StartDate, DateOnly EndDate, decimal? Value, decimal? ValueInRub, string FaceUnit);

public sealed record ClosedSessionAmortization(
    DateOnly Date, decimal? Value, decimal? ValueInRub, decimal? ValuePercent,
    decimal InitialFaceValue, string FaceUnit);

public sealed record ClosedSessionCurrencyRate(DateOnly Date, string Currency, decimal UnitRate);

public sealed record ClosedSessionInstrument(
    string SecId, string BoardId, string CurrencyId, string ShortName, string FaceUnit,
    string BondType, string BondSubType, int? CouponFrequency, decimal InitialFaceValue,
    DateOnly? MaturityDate, IReadOnlyList<ClosedSessionTrade> Trades,
    IReadOnlyList<ClosedSessionCoupon> Coupons, IReadOnlyList<ClosedSessionAmortization> Amortizations,
    IReadOnlyList<ClosedSessionCurrencyRate> CurrencyRates);

/// <summary>Остаток действует с From включительно до To исключительно.</summary>
public sealed record ClosedSessionHolding(DateOnly From, DateOnly To, decimal Quantity);

public sealed record ClosedPositionSession(
    Guid Id, string SecId, string BoardId, string CurrencyId, string ShortName,
    DateOnly StartDate, DateOnly EndDate, int HoldingDays, decimal BoughtQuantity,
    decimal BoughtCleanAmount, decimal SoldCleanAmount, decimal Commission,
    decimal? AmortizationIncome, decimal? BodyPnl, decimal? CouponIncome, decimal? TotalPnl,
    decimal? TotalPnlPercent, decimal? AnnualizedPnlPercent, bool IsComplete,
    string? IncompleteReason, string CalculatorKind, DateTimeOffset CalculatedAt);

public sealed record ClosedSessionIncome(decimal? Amount, string? IncompleteReason = null);
