using Popo.Core.Initialization;

namespace Popo.Jobs.Initialization;

public sealed class InitializationProgressReporter(
    InitializationExecutionContext executionContext,
    IInitializationStateStore stateStore) : IInitializationProgressReporter
{
    public void Begin(string jobKey) => executionContext.JobKey = jobKey;

    public void End() => executionContext.JobKey = null;

    public Task ReportAsync(
        int processedItems,
        int? totalItems,
        string? phase,
        CancellationToken cancellationToken)
    {
        return executionContext.JobKey is null
            ? Task.CompletedTask
            : stateStore.UpdateProgressAsync(
                executionContext.JobKey,
                processedItems,
                totalItems,
                phase,
                cancellationToken);
    }
}
