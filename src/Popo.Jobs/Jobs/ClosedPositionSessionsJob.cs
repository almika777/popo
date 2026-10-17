using Hangfire;
using Popo.Core.Initialization;
using Popo.Core.Portfolio.ClosedSessions;

namespace Popo.Jobs.Jobs;

public sealed class ClosedPositionSessionsJob(
    ClosedPositionSessionsService service,
    IInitializationStateStore initializationStateStore,
    JobStorage jobStorage,
    ILogger<ClosedPositionSessionsJob> logger) : IClosedPositionSessionsJob
{
    public async Task UpdateAsync(CancellationToken cancellationToken = default)
    {
        if (!await initializationStateStore.IsCompleteAsync(cancellationToken))
            throw new InvalidOperationException("Пересчёт закрытых сессий доступен после первичной загрузки данных.");

        using var connection = jobStorage.GetConnection();
        using var calculationLock = connection.AcquireDistributedLock(
            "closed-position-sessions-recalculation", TimeSpan.FromMinutes(15));
        logger.LogInformation("Начат пересчёт закрытых сессий позиций.");
        try
        {
            var count = await service.RecalculateAsync(cancellationToken);
            logger.LogInformation("Пересчёт закрытых сессий завершён: {SessionCount} сессий.", count);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Пересчёт закрытых сессий завершился ошибкой; прежний снимок сохранён.");
            throw;
        }
    }
}
