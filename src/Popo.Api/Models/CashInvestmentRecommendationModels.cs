using FluentValidation;
using Popo.Core.Recommendations;

namespace Popo.Api.Models;

public sealed record InvestmentStrategySettingsRequest(
    CreditRating MinimumRating,
    int? MinimumMaturityDays,
    int? MaximumMaturityDays,
    double MinimumMedianDailyVolume,
    int OfferWindowDays,
    double MaximumYtm = 30d,
    double MinimumYtm = 15d,
    BondInstrumentType InstrumentType = BondInstrumentType.Any,
    string? FaceUnit = null,
    string? CurrencyId = null)
{
    public InvestmentStrategySettings ToDomain() => new(
        MinimumRating,
        MinimumMaturityDays,
        MaximumMaturityDays,
        MinimumMedianDailyVolume,
        OfferWindowDays,
        MaximumYtm,
        MinimumYtm,
        InstrumentType,
        NormalizeCurrency(FaceUnit),
        NormalizeCurrency(CurrencyId));

    private static string? NormalizeCurrency(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}

public sealed record InvestmentStrategySettingsResponse(
    CreditRating MinimumRating,
    int? MinimumMaturityDays,
    int? MaximumMaturityDays,
    double MinimumMedianDailyVolume,
    int OfferWindowDays,
    double MaximumYtm = 30d,
    double MinimumYtm = 15d,
    BondInstrumentType InstrumentType = BondInstrumentType.Any,
    string? FaceUnit = null,
    string? CurrencyId = null);

public sealed record InvestmentStrategyPresetRequest(string Name, InvestmentStrategySettingsRequest Settings);
public sealed record InvestmentStrategyPresetResponse(int Id, string Name, InvestmentStrategySettingsResponse Settings, bool IsActive);

public sealed record CashRecommendationsPageResponse(
    IReadOnlyList<InvestmentStrategyPresetResponse> Presets,
    IReadOnlyList<string> TradingCurrencies,
    IReadOnlyList<string> FaceUnits);

public sealed record CashRecommendationBondResponse(
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
    BondYieldCalculationResponse Calculation);

public sealed record BondYieldCalculationResponse(
    DateOnly SettlementDate,
    double MarketPricePercent,
    double CleanPrice,
    double AccruedInterest,
    double DirtyPrice,
    double FaceValue,
    IReadOnlyList<BondYieldCashFlowResponse> CashFlows);

public sealed record BondYieldCashFlowResponse(
    DateOnly Date,
    double Amount,
    BondYieldCashFlowType Type,
    bool IsProjected);

public sealed record CashRecommendationResponse(
    DateTimeOffset SnapshotTime,
    IReadOnlyList<CashRecommendationBondResponse> Bonds);

public sealed class InvestmentStrategySettingsRequestValidator
    : AbstractValidator<InvestmentStrategySettingsRequest>
{
    public InvestmentStrategySettingsRequestValidator()
    {
        RuleFor(x => x.MinimumRating).IsInEnum();
        RuleFor(x => x.MinimumMaturityDays)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinimumMaturityDays.HasValue);
        RuleFor(x => x.MaximumMaturityDays)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MaximumMaturityDays.HasValue);
        RuleFor(x => x.MaximumMaturityDays)
            .GreaterThanOrEqualTo(x => x.MinimumMaturityDays)
            .When(x => x.MinimumMaturityDays.HasValue && x.MaximumMaturityDays.HasValue);
        RuleFor(x => x.MinimumMedianDailyVolume)
            .Must(double.IsFinite)
            .GreaterThanOrEqualTo(1d);
        RuleFor(x => x.OfferWindowDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaximumYtm)
            .Must(double.IsFinite)
            .InclusiveBetween(double.Epsilon, 100d);
        RuleFor(x => x.MinimumYtm)
            .Must(double.IsFinite)
            .InclusiveBetween(double.Epsilon, 100d)
            .LessThanOrEqualTo(x => x.MaximumYtm);
        RuleFor(x => x.FaceUnit).MaximumLength(16);
        RuleFor(x => x.CurrencyId).MaximumLength(16);
    }
}

public static class CashInvestmentRecommendationApiMapper
{
    public static InvestmentStrategyPresetResponse ToResponse(InvestmentStrategyPreset preset) => new(preset.Id, preset.Name, ToResponse(preset.Settings), preset.IsActive);
    public static InvestmentStrategySettingsResponse ToResponse(InvestmentStrategySettings settings) => new(
        settings.MinimumRating,
        settings.MinimumMaturityDays,
        settings.MaximumMaturityDays,
        settings.MinimumMedianDailyVolume,
        settings.OfferWindowDays,
        settings.MaximumYtm,
        settings.MinimumYtm,
        settings.InstrumentType,
        settings.FaceUnit,
        settings.CurrencyId);

}

public static class CashRecommendationApiMapper
{
    public static CashRecommendationResponse ToResponse(
        IReadOnlyList<BondCandidate> candidates) => new(
        DateTimeOffset.UtcNow,
        candidates
            .Select(x => new CashRecommendationBondResponse(
                x.SecId,
                x.BoardId,
                x.IssuerId,
                x.IssuerName,
                x.CurrencyId,
                x.Ytm,
                x.BuyPrice,
                x.Rating,
                x.MaturityDate,
                x.OfferDate,
                x.CouponType,
                x.MedianDailyVolume,
                x.YieldDate,
                x.YieldDateType,
                new BondYieldCalculationResponse(
                    x.Calculation.SettlementDate,
                    x.Calculation.MarketPricePercent,
                    x.Calculation.CleanPrice,
                    x.Calculation.AccruedInterest,
                    x.Calculation.DirtyPrice,
                    x.Calculation.FaceValue,
                    x.Calculation.CashFlows
                        .Select(flow => new BondYieldCashFlowResponse(
                            flow.Date,
                            flow.Amount,
                            flow.Type,
                            flow.IsProjected))
                        .ToArray())))
            .ToArray());
}
