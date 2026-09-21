using Popo.Core.Portfolio;

namespace Popo.Core.Recommendations;

public sealed class PositionRecommendationService(
    IPortfolioLedgerProvider portfolioLedgerProvider,
    ICashInvestmentRecommendationStore settingsStore,
    IBondAssessmentService assessmentService) : IPositionRecommendationService
{
    public async Task<PositionRecommendationSnapshot> CalculateAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsStore.GetSettingsAsync(cancellationToken);
        var positions = (await portfolioLedgerProvider.GetPositionsAsync(cancellationToken))
            .Where(x => x.Quantity > 0)
            .ToArray();
        var assessments = (await assessmentService.GetAssessmentsAsync(settings, cancellationToken))
            .ToDictionary(x => (x.SecId, x.BoardId));

        var recommendations = positions.Select(position =>
        {
            if (!assessments.TryGetValue((position.SecId, position.BoardId), out var assessment))
            {
                return new PositionRecommendation(
                    position.SecId, position.BoardId, PositionRecommendationAction.Unavailable,
                    position.Quantity, null, null, null, null, null, null,
                    [PositionRecommendationReason.MissingData]);
            }

            var bestComparable = FindBestComparable(assessment, assessments.Values, settings);

            return PositionRecommendationPolicy.Evaluate(
                position.Quantity, assessment, bestComparable, settings);
        }).ToArray();

        return new PositionRecommendationSnapshot(DateTimeOffset.UtcNow, recommendations);
    }

    internal static BondAssessment? FindBestComparable(
        BondAssessment assessment,
        IEnumerable<BondAssessment> assessments,
        InvestmentStrategySettings settings) =>
        assessments
            .Where(candidate => candidate.Candidate is { } candidateDetails &&
                assessment.Candidate is { } assessmentDetails &&
                candidateDetails.CouponType == assessmentDetails.CouponType &&
                (candidate.SecId != assessment.SecId || candidate.BoardId != assessment.BoardId) &&
                candidate.CurrencyId == assessment.CurrencyId &&
                candidate.Rating >= assessment.Rating &&
                candidate.MedianDailyVolume >= settings.MinimumMedianDailyVolume &&
                candidate.YieldDate is { } candidateDate && assessment.YieldDate is { } positionDate &&
                Math.Abs(candidateDate.DayNumber - positionDate.DayNumber) <= 30)
            .OrderByDescending(candidate => candidate.Ytm)
            .FirstOrDefault();
}

public interface IPositionRecommendationService
{
    Task<PositionRecommendationSnapshot> CalculateAsync(CancellationToken cancellationToken);
}
