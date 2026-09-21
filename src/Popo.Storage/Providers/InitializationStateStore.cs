using Microsoft.EntityFrameworkCore;
using Popo.Core.Initialization;
using Popo.Storage.Entities;

namespace Popo.Storage.Providers;

public sealed class InitializationStateStore(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IInitializationStateStore
{
    public async Task EnsureCurrentBootstrapAsync(CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingKeys = await context.InitializationJobStates
            .Where(x => x.BootstrapVersion == InitializationBootstrap.CurrentVersion)
            .Select(x => x.JobKey)
            .ToHashSetAsync(cancellationToken);

        var missing = InitializationBootstrap.Jobs
            .Where(x => !existingKeys.Contains(x.Key))
            .Select(x => new InitializationJobStateEntity
            {
                BootstrapVersion = InitializationBootstrap.CurrentVersion,
                JobKey = x.Key,
                Status = InitializationJobStatus.Pending,
                UpdatedAt = DateTimeOffset.UtcNow
            })
            .ToArray();

        if (missing.Length == 0)
        {
            return;
        }

        await context.InitializationJobStates.AddRangeAsync(missing, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecoverUnfinishedJobsAsync(CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var jobs = await context.InitializationJobStates
            .AsTracking()
            .Where(x => x.BootstrapVersion == InitializationBootstrap.CurrentVersion
                        && (x.Status == InitializationJobStatus.Running || x.Status == InitializationJobStatus.Failed))
            .ToListAsync(cancellationToken);

        if (jobs.Count == 0)
        {
            return;
        }

        foreach (var job in jobs)
        {
            ResetToPending(job);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<InitializationStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var states = await context.InitializationJobStates
            .AsNoTracking()
            .Where(x => x.BootstrapVersion == InitializationBootstrap.CurrentVersion)
            .ToDictionaryAsync(x => x.JobKey, cancellationToken);

        var result = InitializationBootstrap.Jobs
            .Select(definition => states.TryGetValue(definition.Key, out var state)
                ? ToContract(definition, state)
                : NewPendingState(definition))
            .ToArray();
        var isComplete = result.All(x => x.Status == InitializationJobStatus.Succeeded);

        return new InitializationStatus(
            InitializationBootstrap.CurrentVersion,
            !isComplete,
            isComplete,
            result);
    }

    public async Task<bool> IsCompleteAsync(CancellationToken cancellationToken)
    {
        var status = await GetStatusAsync(cancellationToken);
        return status.IsComplete;
    }

    public async Task<string?> GetNextPendingJobKeyAsync(CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var pendingKeys = await context.InitializationJobStates
            .AsNoTracking()
            .Where(x => x.BootstrapVersion == InitializationBootstrap.CurrentVersion
                        && x.Status == InitializationJobStatus.Pending)
            .Select(x => x.JobKey)
            .ToHashSetAsync(cancellationToken);

        return InitializationBootstrap.Jobs
            .Select(x => x.Key)
            .FirstOrDefault(pendingKeys.Contains);
    }

    public async Task<bool> StartJobAsync(string jobKey, CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await FindStateAsync(context, jobKey, cancellationToken);
        if (state is null || state.Status != InitializationJobStatus.Pending)
        {
            return false;
        }

        state.Status = InitializationJobStatus.Running;
        state.Attempts++;
        state.ProcessedItems = 0;
        state.TotalItems = null;
        state.Phase = "Подготовка";
        state.StartedAt = DateTimeOffset.UtcNow;
        state.CompletedAt = null;
        state.LastError = null;
        state.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UpdateProgressAsync(
        string jobKey,
        int processedItems,
        int? totalItems,
        string? phase,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await FindStateAsync(context, jobKey, cancellationToken);
        if (state is null || state.Status != InitializationJobStatus.Running)
        {
            return;
        }

        state.ProcessedItems = Math.Max(0, processedItems);
        state.TotalItems = totalItems is null ? null : Math.Max(0, totalItems.Value);
        state.Phase = phase;
        state.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSucceededAsync(string jobKey, CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await FindStateAsync(context, jobKey, cancellationToken);
        if (state is null)
        {
            return;
        }

        state.Status = InitializationJobStatus.Succeeded;
        state.TotalItems ??= state.ProcessedItems;
        state.ProcessedItems = state.TotalItems ?? state.ProcessedItems;
        state.Phase = "Завершено";
        state.CompletedAt = DateTimeOffset.UtcNow;
        state.LastError = null;
        state.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(string jobKey, string error, CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await FindStateAsync(context, jobKey, cancellationToken);
        if (state is null)
        {
            return;
        }

        state.Status = InitializationJobStatus.Failed;
        state.Phase = "Ошибка";
        state.LastError = error.Length > 4000 ? error[..4000] : error;
        state.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RetryFailedJobAutomaticallyAsync(string jobKey, CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await FindStateAsync(context, jobKey, cancellationToken);
        if (state is null || state.Status != InitializationJobStatus.Failed || state.Attempts >= 3)
        {
            return false;
        }

        state.Status = InitializationJobStatus.Pending;
        state.ProcessedItems = 0;
        state.TotalItems = null;
        state.Phase = "Автоматический повтор";
        state.LastError = null;
        state.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<InitializationJobStatus?> RetryJobAsync(string jobKey, CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await FindStateAsync(context, jobKey, cancellationToken);
        if (state is null)
        {
            return null;
        }

        if (state.Status == InitializationJobStatus.Failed)
        {
            ResetToPending(state);
            await context.SaveChangesAsync(cancellationToken);
        }

        return state.Status;
    }

    private static async Task<InitializationJobStateEntity?> FindStateAsync(
        PopoDbContext context,
        string jobKey,
        CancellationToken cancellationToken) =>
        await context.InitializationJobStates.AsTracking().SingleOrDefaultAsync(
            x => x.BootstrapVersion == InitializationBootstrap.CurrentVersion && x.JobKey == jobKey,
            cancellationToken);

    private static InitializationJobState ToContract(
        InitializationJobDefinition definition,
        InitializationJobStateEntity state) =>
        new(
            definition.Key,
            definition.DisplayName,
            state.Status,
            state.ProcessedItems,
            state.TotalItems,
            state.Phase,
            state.Attempts,
            state.StartedAt,
            state.CompletedAt,
            state.LastError,
            state.UpdatedAt);

    private static InitializationJobState NewPendingState(InitializationJobDefinition definition) =>
        new(
            definition.Key,
            definition.DisplayName,
            InitializationJobStatus.Pending,
            0,
            null,
            "Ожидание запуска",
            0,
            null,
            null,
            null,
            DateTimeOffset.UtcNow);

    private static void ResetToPending(InitializationJobStateEntity state)
    {
        state.Status = InitializationJobStatus.Pending;
        state.ProcessedItems = 0;
        state.TotalItems = null;
        state.Phase = "Ожидание запуска";
        state.Attempts = 0;
        state.StartedAt = null;
        state.CompletedAt = null;
        state.LastError = null;
        state.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
