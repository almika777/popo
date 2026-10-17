using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Popo.Core.Common;
using Popo.Core.Initialization;
using Popo.Core.Portfolio.ClosedSessions;

namespace Popo.Api.Services.ClosedPositions;

public sealed record ClosedPositionSessionsJobResponse(string JobId);
public sealed record ClosedPositionSessionsJobStatus(string JobId, string State);

public sealed class ClosedPositionSessionsPageService(
    IClosedPositionSessionsProvider provider,
    IInitializationStateStore initializationStateStore,
    IBackgroundJobClient jobClient,
    JobStorage jobStorage)
{
    public Task<IReadOnlyList<ClosedPositionSession>> GetAsync(CancellationToken cancellationToken)
        => provider.GetAsync(cancellationToken);

    public async Task<ClosedPositionSessionsJobResponse> RecalculateAsync(CancellationToken cancellationToken)
    {
        if (!await initializationStateStore.IsCompleteAsync(cancellationToken))
            throw new ApplicationConflictException("Дождитесь завершения первичной загрузки данных.");

        var id = jobClient.Create(Job.FromExpression<IClosedPositionSessionsJob>(
            job => job.UpdateAsync(CancellationToken.None)), new EnqueuedState("fast"));
        return new ClosedPositionSessionsJobResponse(id);
    }

    public ClosedPositionSessionsJobStatus GetStatus(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId) || jobId.Length > 64 || !jobId.All(char.IsAsciiDigit))
            throw new InvalidApplicationRequestException("Некорректный идентификатор задачи.");
        using var connection = jobStorage.GetConnection();
        var data = connection.GetJobData(jobId);
        if (data?.Job?.Type != typeof(IClosedPositionSessionsJob))
            throw new ApplicationResourceNotFoundException("Задача пересчёта закрытых сессий не найдена.");
        return new ClosedPositionSessionsJobStatus(jobId, connection.GetStateData(jobId)?.Name ?? "Enqueued");
    }
}
