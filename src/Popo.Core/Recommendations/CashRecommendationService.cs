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
}

public interface ICashRecommendationService
{
    Task<List<BondCandidate>> GetRecommendations(CancellationToken ct);
}
