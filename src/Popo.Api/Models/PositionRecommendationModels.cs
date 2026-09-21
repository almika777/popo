using Popo.Core.Recommendations;

namespace Popo.Api.Models;

public sealed record PositionRecommendationItemResponse(
    string SecId,
    string BoardId,
    PositionRecommendationAction Action,
    double Quantity,
    double? Ytm,
    double? CalculationPrice,
    CreditRating? Rating,
    double? AverageDailyVolume,
    double? PositionVolumeShare,
    PositionRecommendationAlternativeResponse? Alternative,
    IReadOnlyList<PositionRecommendationReason> Reasons);

public sealed record PositionRecommendationAlternativeResponse(
    string SecId,
    string BoardId,
    string? ShortName,
    double Ytm,
    double CalculationPrice,
    CreditRating Rating,
    DateOnly YieldDate);

public sealed record PositionRecommendationStateResponse(
    RecommendationStatus Status,
    DateTimeOffset? CalculationStartedAt,
    DateTimeOffset? LastSuccessfulAt,
    DateTimeOffset? SnapshotTime,
    string? Error,
    IReadOnlyList<PositionRecommendationItemResponse> Recommendations);

public static class PositionRecommendationApiMapper
{
    public static PositionRecommendationStateResponse ToResponse(PositionRecommendationState state) => new(
        state.Status,
        state.CalculationStartedAt,
        state.LastSuccessfulAt,
        state.SnapshotTime,
        state.Error,
        state.Snapshot?.Recommendations.Select(x => new PositionRecommendationItemResponse(
            x.SecId, x.BoardId, x.Action, x.Quantity, x.Ytm, x.CalculationPrice, x.Rating,
            x.AverageDailyVolume, x.PositionVolumeShare,
            x.Alternative is null ? null : new PositionRecommendationAlternativeResponse(
                x.Alternative.SecId, x.Alternative.BoardId, x.Alternative.ShortName,
                x.Alternative.Ytm, x.Alternative.CalculationPrice,
                x.Alternative.Rating, x.Alternative.YieldDate),
            x.Reasons)).ToArray() ?? []);
}
