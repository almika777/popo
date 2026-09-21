using Microsoft.EntityFrameworkCore;
using Polly;
using Popo.Jobs.Initialization;
using Popo.Storage;

namespace Popo.Jobs.Jobs;

public abstract class JobBase(
    IDbContextFactory<PopoDbContext> dbContextFactory,
    ILogger logger,
    IInitializationProgressReporter progressReporter)
{
    private const int RetryAttempts = 3;
    protected const int BatchSize = 250;

    protected async Task<PopoDbContext> CreateDbContextAsync(CancellationToken ct)
    {
        return await dbContextFactory.CreateDbContextAsync(ct);
    }

    protected Task ReportProgressAsync(
        int processedItems,
        int? totalItems,
        string phase,
        CancellationToken cancellationToken) =>
        progressReporter.ReportAsync(processedItems, totalItems, phase, cancellationToken);

    protected async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName,
        CancellationToken ct)
    {
        var policy = Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .Or<DbUpdateException>()
            .WaitAndRetryAsync(
                retryCount: RetryAttempts - 1,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)),
                onRetry: (exception, delay, attempt, _) =>
                {
                    logger.LogWarning(
                        exception,
                        "Операция {Operation} завершилась ошибкой. Повторная попытка {RetryAttempt}/{RetryCount} через {Delay} мс.",
                        operationName,
                        attempt,
                        RetryAttempts - 1,
                        delay.TotalMilliseconds);
                });

        return await policy.ExecuteAsync((_, _) => operation(), new Context(), ct);
    }
}
