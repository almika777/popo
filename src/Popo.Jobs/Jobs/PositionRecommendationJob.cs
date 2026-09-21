using Hangfire;
using Popo.Core.Initialization;
using Popo.Core.Recommendations;

namespace Popo.Jobs.Jobs;

public sealed class PositionRecommendationJob(
    IPositionRecommendationService service,
    IPositionRecommendationStore store,
    IInitializationStateStore initializationStateStore,
    ILogger<PositionRecommendationJob> logger) : IPositionRecommendationJob
{
    [DisableConcurrentExecution(15 * 60)]
    public async Task UpdateAsync(CancellationToken ct = default)
    {
        if (!await initializationStateStore.IsCompleteAsync(ct))
        {
            logger.LogInformation("Расчёт рекомендаций по позициям пропущен до завершения первичной загрузки.");
            return;
        }

        await store.StartAsync(DateTimeOffset.UtcNow, ct);
        try
        {
            var snapshot = await service.CalculateAsync(ct);
            await store.CompleteAsync(snapshot, ct);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Расчёт рекомендаций по позициям завершился ошибкой.");
            await store.FailAsync("Не удалось рассчитать рекомендации по позициям. Подробности доступны в журнале.", ct);
            throw;
        }
    }
}

public interface IPositionRecommendationJob : IHangfireJob;
