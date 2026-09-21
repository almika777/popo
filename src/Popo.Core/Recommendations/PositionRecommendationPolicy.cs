namespace Popo.Core.Recommendations;

public enum PositionRecommendationAction
{
    Buy,
    Hold,
    Sell,
    Unavailable
}

public enum PositionRecommendationReason
{
    MissingData,
    RatingBelowMinimum,
    YtmBelowMinimum,
    YtmAboveMaximum,
    PositionLiquidityLimitExceeded,
    PositionLiquidityLimitNear,
    MaturityOutsideRange,
    OfferTooClose,
    InstrumentTypeMismatch,
    MinimumLiquidityNotMet,
    BetterMarketAlternative,
    CurrentYieldAdvantage,
    NoMaterialYieldDifference,
    NoComparableAlternative
}

public sealed record PositionRecommendation(
    string SecId,
    string BoardId,
    PositionRecommendationAction Action,
    double Quantity,
    double? Ytm,
    double? CalculationPrice,
    CreditRating? Rating,
    double? AverageDailyVolume,
    double? PositionVolumeShare,
    PositionRecommendationAlternative? Alternative,
    IReadOnlyList<PositionRecommendationReason> Reasons);

public sealed record PositionRecommendationAlternative(
    string SecId,
    string BoardId,
    string? ShortName,
    double Ytm,
    double CalculationPrice,
    CreditRating Rating,
    DateOnly YieldDate);

public static class PositionRecommendationPolicy
{
    public static PositionRecommendation Evaluate(
        double quantity,
        BondAssessment assessment,
        BondAssessment? bestComparable,
        InvestmentStrategySettings settings)
    {
        if (assessment.Ytm is null || assessment.Rating is null ||
            assessment.AverageDailyVolume is null or <= 0)
        {
            return Create(PositionRecommendationAction.Unavailable, null, null,
                [PositionRecommendationReason.MissingData]);
        }

        var share = quantity / assessment.AverageDailyVolume.Value;
        var bestComparableYtm = bestComparable?.Ytm;
        var sellReasons = new List<PositionRecommendationReason>();
        if (assessment.Ytm > settings.MaximumYtm) sellReasons.Add(PositionRecommendationReason.YtmAboveMaximum);
        if (share >= 0.1d) sellReasons.Add(PositionRecommendationReason.PositionLiquidityLimitExceeded);
        if (bestComparableYtm >= assessment.Ytm + 0.5d) sellReasons.Add(PositionRecommendationReason.BetterMarketAlternative);
        if (sellReasons.Count > 0) return Create(PositionRecommendationAction.Sell, share, bestComparableYtm, sellReasons);

        if (share >= 0.08d)
            return Create(PositionRecommendationAction.Hold, share, bestComparableYtm,
                [PositionRecommendationReason.PositionLiquidityLimitNear]);

        if (bestComparableYtm is null)
            return Create(PositionRecommendationAction.Hold, share, null,
                [PositionRecommendationReason.NoComparableAlternative]);

        if (assessment.Ytm >= bestComparableYtm + 0.5d)
            return Create(PositionRecommendationAction.Buy, share, bestComparableYtm,
                [PositionRecommendationReason.CurrentYieldAdvantage]);

        return Create(PositionRecommendationAction.Hold, share, bestComparableYtm,
            [PositionRecommendationReason.NoMaterialYieldDifference]);

        PositionRecommendation Create(
            PositionRecommendationAction action,
            double? positionVolumeShare,
            double? comparableYtm,
            IReadOnlyList<PositionRecommendationReason> reasons) => new(
            assessment.SecId,
            assessment.BoardId,
            action,
            quantity,
            assessment.Ytm,
            assessment.Candidate?.Calculation.CleanPrice,
            assessment.Rating,
            assessment.AverageDailyVolume,
            positionVolumeShare,
            bestComparable is { Ytm: { } ytm, Rating: { } rating, YieldDate: { } yieldDate, Candidate: { } comparableCandidate }
                ? new PositionRecommendationAlternative(
                    bestComparable.SecId, bestComparable.BoardId, bestComparable.ShortName,
                    ytm, comparableCandidate.Calculation.CleanPrice, rating, yieldDate)
                : null,
            reasons);
    }
}
