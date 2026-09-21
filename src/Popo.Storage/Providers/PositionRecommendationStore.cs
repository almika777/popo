using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Popo.Core.Recommendations;
using Popo.Storage.Entities;

namespace Popo.Storage.Providers;

public sealed class PositionRecommendationStore(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IPositionRecommendationStore
{
    private const int StateId = 1;

    public async Task<PositionRecommendationState> GetAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.PositionRecommendationStates.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == StateId, cancellationToken);
        return entity is null
            ? new PositionRecommendationState(RecommendationStatus.Unavailable, null, null, null, null, null)
            : ToDomain(entity);
    }

    public Task StartAsync(DateTimeOffset startedAt, CancellationToken cancellationToken) =>
        UpdateAsync(entity =>
        {
            entity.Status = RecommendationStatus.Calculating;
            entity.CalculationStartedAt = startedAt;
            entity.Error = null;
        }, cancellationToken);

    public Task CompleteAsync(PositionRecommendationSnapshot snapshot, CancellationToken cancellationToken) =>
        UpdateAsync(entity =>
        {
            entity.Status = RecommendationStatus.Ready;
            entity.CalculationStartedAt = null;
            entity.LastSuccessfulAt = snapshot.SnapshotTime;
            entity.SnapshotTime = snapshot.SnapshotTime;
            entity.Error = null;
            entity.ResultJson = JsonSerializer.Serialize(snapshot);
        }, cancellationToken);

    public Task FailAsync(string error, CancellationToken cancellationToken) =>
        UpdateAsync(entity =>
        {
            entity.Status = entity.ResultJson is null ? RecommendationStatus.Unavailable : RecommendationStatus.Stale;
            entity.CalculationStartedAt = null;
            entity.Error = error;
        }, cancellationToken);

    private async Task UpdateAsync(Action<PositionRecommendationStateEntity> update, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.PositionRecommendationStates.AsTracking()
            .SingleOrDefaultAsync(x => x.Id == StateId, cancellationToken);
        if (entity is null)
        {
            entity = new PositionRecommendationStateEntity { Id = StateId };
            db.PositionRecommendationStates.Add(entity);
        }
        update(entity);
        entity.Version++;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static PositionRecommendationState ToDomain(PositionRecommendationStateEntity entity) => new(
        entity.Status,
        entity.CalculationStartedAt,
        entity.LastSuccessfulAt,
        entity.SnapshotTime,
        entity.Error,
        entity.ResultJson is null
            ? null
            : JsonSerializer.Deserialize<PositionRecommendationSnapshot>(entity.ResultJson));
}

