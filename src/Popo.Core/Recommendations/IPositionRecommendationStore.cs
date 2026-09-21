namespace Popo.Core.Recommendations;

public enum RecommendationStatus
{
    Calculating,
    Ready,
    Stale,
    Unavailable
}

public sealed record PositionRecommendationSnapshot(
    DateTimeOffset SnapshotTime,
    IReadOnlyList<PositionRecommendation> Recommendations);

public sealed record PositionRecommendationState(
    RecommendationStatus Status,
    DateTimeOffset? CalculationStartedAt,
    DateTimeOffset? LastSuccessfulAt,
    DateTimeOffset? SnapshotTime,
    string? Error,
    PositionRecommendationSnapshot? Snapshot);

public interface IPositionRecommendationStore
{
    Task<PositionRecommendationState> GetAsync(CancellationToken cancellationToken);
    Task StartAsync(DateTimeOffset startedAt, CancellationToken cancellationToken);
    Task CompleteAsync(PositionRecommendationSnapshot snapshot, CancellationToken cancellationToken);
    Task FailAsync(string error, CancellationToken cancellationToken);
}
