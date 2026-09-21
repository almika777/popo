using Popo.Core.Recommendations;

namespace Popo.Storage.Entities;

public sealed class PositionRecommendationStateEntity
{
    public int Id { get; set; }
    public RecommendationStatus Status { get; set; }
    public DateTimeOffset? CalculationStartedAt { get; set; }
    public DateTimeOffset? LastSuccessfulAt { get; set; }
    public DateTimeOffset? SnapshotTime { get; set; }
    public string? Error { get; set; }
    public string? ResultJson { get; set; }
    public int Version { get; set; }
}

