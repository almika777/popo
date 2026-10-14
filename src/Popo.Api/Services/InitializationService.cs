using Popo.Core.Common;
using Popo.Core.Initialization;

namespace Popo.Api.Services;

public sealed class InitializationService(IInitializationStateStore stateStore)
{
    public Task<InitializationStatus> GetStatusAsync(CancellationToken cancellationToken) =>
        stateStore.GetStatusAsync(cancellationToken);

    public async Task<InitializationStatus> RetryJobAsync(string jobKey, CancellationToken cancellationToken)
    {
        if (!InitializationBootstrap.TryGetJob(jobKey, out _))
            throw new ApplicationResourceNotFoundException();

        var status = await stateStore.RetryJobAsync(jobKey, cancellationToken)
            ?? throw new ApplicationResourceNotFoundException();
        if (status != InitializationJobStatus.Pending)
            throw new ApplicationConflictException($"Задачу нельзя повторить в состоянии {status}.");

        return await stateStore.GetStatusAsync(cancellationToken);
    }
}
