namespace Popo.Core.Recommendations;

public enum BondAssessmentIssue
{
    MissingSecurityData,
    MissingMarketPrice,
    MissingRating,
    MissingLiquidity,
    MissingYieldSchedule,
    InvalidYield
}

public sealed record BondAssessment(
    string SecId,
    string BoardId,
    string? ShortName,
    string? CurrencyId,
    string? FaceUnit,
    DateOnly? YieldDate,
    double? Ytm,
    CreditRating? Rating,
    double? AverageDailyVolume,
    double? MedianDailyVolume,
    bool MeetsMaturityRange,
    bool MeetsOfferWindow,
    bool MeetsInstrumentType,
    BondCandidate? Candidate,
    IReadOnlyList<BondAssessmentIssue> Issues);
