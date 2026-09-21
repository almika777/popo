using EFCore.BulkExtensions;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Popo.Core.HttpClients;
using Popo.Jobs.Initialization;
using Popo.Storage;
using Popo.Storage.Entities.Moex;

namespace Popo.Jobs.Jobs;

public sealed class MoexEmitentUpdateJob(
    IMoexHttpClient moexHttpClient,
    IDbContextFactory<PopoDbContext> dbContextFactory,
    ILogger<MoexBondUpdateJob> logger,
    IInitializationProgressReporter progressReporter)
    : JobBase(dbContextFactory, logger, progressReporter), IMoexEmitentUpdateJob
{
    public async Task UpdateAsync(CancellationToken ct = default)
    {
        var emitents = await ExecuteWithRetryAsync(
            () => moexHttpClient.GetAllEmitentDescriptionAsync(ct),
            "получение списка эмитентов MOEX", ct);

        var entities = emitents.Select(x => x.Adapt<MoexEmitentEntity>());
        await ReportProgressAsync(0, emitents.Count, "Сохранение эмитентов", ct);

        await using var ctx = await CreateDbContextAsync(ct);
        await ctx.BulkInsertOrUpdateAsync(entities, config =>
        {
            config.UpdateByProperties = [nameof(MoexEmitentEntity.Id)];
            config.PropertiesToExcludeOnUpdate = [nameof(MoexEmitentEntity.Id)];
        }, cancellationToken: ct);
        await ReportProgressAsync(emitents.Count, emitents.Count, "Сохранение эмитентов", ct);
    }
}

public interface IMoexEmitentUpdateJob : IHangfireJob
{
}
