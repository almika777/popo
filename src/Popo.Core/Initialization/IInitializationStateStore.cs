namespace Popo.Core.Initialization;

public interface IInitializationStateStore
{
    Task EnsureCurrentBootstrapAsync(CancellationToken cancellationToken);

    Task RecoverUnfinishedJobsAsync(CancellationToken cancellationToken);

    Task<InitializationStatus> GetStatusAsync(CancellationToken cancellationToken);

    Task<bool> IsCompleteAsync(CancellationToken cancellationToken);

    Task<string?> GetNextPendingJobKeyAsync(CancellationToken cancellationToken);

    Task<bool> StartJobAsync(string jobKey, CancellationToken cancellationToken);

    Task UpdateProgressAsync(
        string jobKey,
        int processedItems,
        int? totalItems,
        string? phase,
        CancellationToken cancellationToken);

    Task MarkSucceededAsync(string jobKey, CancellationToken cancellationToken);

    Task MarkFailedAsync(string jobKey, string error, CancellationToken cancellationToken);

    Task<bool> RetryFailedJobAutomaticallyAsync(string jobKey, CancellationToken cancellationToken);

    Task<InitializationJobStatus?> RetryJobAsync(string jobKey, CancellationToken cancellationToken);
}
