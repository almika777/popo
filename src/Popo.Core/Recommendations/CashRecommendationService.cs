using Popo.Core.Bonds;

namespace Popo.Core.Recommendations;

public class CashRecommendationService(
    ICashInvestmentRecommendationStore cashInvestmentRecommendationStore,
    IBondAssessmentService bondAssessmentService) : ICashRecommendationService
{
    public async Task<List<BondCandidate>> GetRecommendations(CancellationToken ct)
    {
        var settings = await cashInvestmentRecommendationStore.GetSettingsAsync(ct);
        var assessments = await bondAssessmentService.GetAssessmentsAsync(settings, ct);
        return assessments
            .Where(x => x.Candidate is not null && x.MeetsMaturityRange && x.MeetsOfferWindow &&
                x.MeetsInstrumentType && x.Rating >= settings.MinimumRating &&
                IsAllowedByCurrencies(x.FaceUnit, x.CurrencyId, settings.FaceUnit, settings.CurrencyId) &&
                x.Ytm >= settings.MinimumYtm && x.Ytm <= settings.MaximumYtm &&
                x.MedianDailyVolume >= settings.MinimumMedianDailyVolume)
            .Select(x => x.Candidate!.Value)
            .ToList();
    }

    internal static bool IsAllowedByInstrumentType(BondSecurityDto security, BondInstrumentType instrumentType) =>
        instrumentType switch
        {
            BondInstrumentType.Floating => security.IsFloater,
            BondInstrumentType.Fixed => !security.IsFloater,
            _ => true
        };

    internal static bool IsAllowedByCurrencies(
        BondSecurityDto security,
        string? faceUnit,
        string? currencyId) =>
        IsAllowedByCurrencies(security.FaceUnit, security.CurrencyId, faceUnit, currencyId);

    private static bool IsAllowedByCurrencies(
        string? actualFaceUnit,
        string? actualCurrencyId,
        string? faceUnit,
        string? currencyId) =>
        (faceUnit is null || string.Equals(actualFaceUnit, faceUnit, StringComparison.OrdinalIgnoreCase)) &&
        (currencyId is null || string.Equals(actualCurrencyId, currencyId, StringComparison.OrdinalIgnoreCase));

    internal static BondYieldCalculationDetails CreateCalculationDetails(
        DateOnly settlementDate,
        double marketPricePercent,
        double accruedInterest,
        double dirtyPrice,
        BondYieldSchedule schedule) => new(
        settlementDate,
        marketPricePercent,
        dirtyPrice - accruedInterest,
        accruedInterest,
        dirtyPrice,
        schedule.FaceValueInSettlementCurrency,
        schedule.CashFlowDetails);

    internal static bool TryCreateYieldRequest(
        BondSecurityDto security,
        DateOnly today,
        out BondYieldRequest request)
    {
        request = null!;
        if (security.SettleDate is not { } settlementDate ||
            settlementDate < today ||
            security.MatDate is not { } maturityDate ||
            maturityDate <= settlementDate ||
            string.IsNullOrWhiteSpace(security.CurrencyId))
        {
            return false;
        }

        var offerDate = new[] { security.BuybackDate, security.OfferDate }
            .Where(x => x is { } date && date > settlementDate && date < maturityDate)
            .Min();
        var yieldDateType = offerDate is null
            ? BondYieldDateType.Maturity
            : BondYieldDateType.Offer;
        if (yieldDateType == BondYieldDateType.Offer &&
            (security.BuybackPrice is not { } offerPrice ||
             !double.IsFinite(offerPrice) ||
             offerPrice <= 0d))
        {
            return false;
        }

        request = new BondYieldRequest(
            security.SecId,
            security.BoardId,
            security.Isin,
            settlementDate,
            offerDate ?? maturityDate,
            yieldDateType,
            security.FaceValue,
            security.FaceUnit,
            security.CurrencyId,
            yieldDateType == BondYieldDateType.Offer ? security.BuybackPrice : null,
            security.RequiresCouponSchedule,
            security.RequiresExplicitRedemption);
        return true;
    }

    internal static CreditRating? GetRecommendationRating(RatingsDto ratings)
    {
        var values = new[] { ratings.Acra, ratings.Expert, ratings.Nra, ratings.Nkr }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToArray();
        return values.Length == 0 ? null : values.Max();
    }
}

public interface ICashRecommendationService
{
    Task<List<BondCandidate>> GetRecommendations(CancellationToken ct);
}
