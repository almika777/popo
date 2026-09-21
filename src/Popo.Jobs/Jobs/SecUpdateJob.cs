using EFCore.BulkExtensions;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Popo.Core.HttpClients;
using Popo.Jobs.Initialization;
using Popo.Storage;
using Popo.Storage.Entities.Moex;

namespace Popo.Jobs.Jobs;

public sealed class SecUpdateJob(
    IMoexHttpClient moexHttpClient,
    IDbContextFactory<PopoDbContext> dbContextFactory,
    ILogger<MoexBondUpdateJob> logger,
    IInitializationProgressReporter progressReporter)
    : JobBase(dbContextFactory, logger, progressReporter), ISecUpdateJob
{
    public async Task UpdateAsync(CancellationToken ct = default)
    {
        var secs = await ExecuteWithRetryAsync(
            () => moexHttpClient.GetActiveSecAsync(ct),
            "", ct);

        var entities = secs.Select(x => x.Adapt<SecInfoEntity>());
        await ReportProgressAsync(0, secs.Count, "Сохранение активных SEC", ct);

        await using var ctx = await CreateDbContextAsync(ct);
        await ctx.BulkInsertOrUpdateAsync(entities,
            config => { config.UpdateByProperties = [nameof(SecInfoEntity.SecId)]; }, cancellationToken: ct);
        await ReportProgressAsync(secs.Count, secs.Count, "Сохранение активных SEC", ct);
    }
}

public interface ISecUpdateJob : IHangfireJob
{
}
