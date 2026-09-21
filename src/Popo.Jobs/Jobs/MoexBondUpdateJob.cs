using System.Collections.Concurrent;
using EFCore.BulkExtensions;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Popo.Core.HttpClients;
using Popo.Jobs.Initialization;
using Popo.Storage;
using Popo.Storage.Entities.Moex;

namespace Popo.Jobs.Jobs;

public sealed class MoexBondUpdateJob(
    IMoexHttpClient moexHttpClient,
    IDbContextFactory<PopoDbContext> dbContextFactory,
    ILogger<MoexBondUpdateJob> logger,
    IInitializationProgressReporter progressReporter)
    : JobBase(dbContextFactory, logger, progressReporter), IMoexBondUpdateJob
{
    public async Task UpdateAsync(CancellationToken ct = default)
    {
        var moexSecIds = await ExecuteWithRetryAsync(() => LoadSourceAsync(ct), "получение списка бумаг MOEX", ct);
        await ReportProgressAsync(0, moexSecIds.Length, "Получение описаний облигаций", ct);

        var entities = new ConcurrentBag<MoexBondEntity>();
        var failedSecIds = new ConcurrentBag<string>();
        var processed = 0;
        await Parallel.ForEachAsync(moexSecIds, new ParallelOptions { MaxDegreeOfParallelism = 2 }, async (s, token) =>
        {
            try
            {
                var description = await moexHttpClient.GetSecurityDescriptionAsync(s, token);
                var entity = description.Adapt<MoexBondEntity>();
                entity.Id = Guid.CreateVersion7();
                entity.Updated = DateTimeOffset.UtcNow;
                entity.FaceUnit = NormalizeCurrency(entity.FaceUnit);
                entities.Add(entity);

                var processedCount = Interlocked.Increment(ref processed);
                if (processedCount % 10 == 0 || processedCount == moexSecIds.Length)
                    await ReportProgressAsync(processedCount, moexSecIds.Length, "Получение описаний облигаций", token);
                logger.LogInformation("Обработано облигаций MOEX: {Processed}/{Total}", processedCount,
                    moexSecIds.Length);
            }
            catch (Exception ex)
            {
                failedSecIds.Add(s);
                var processedCount = Interlocked.Increment(ref processed);
                if (processedCount % 10 == 0 || processedCount == moexSecIds.Length)
                    await ReportProgressAsync(processedCount, moexSecIds.Length, "Получение описаний облигаций", token);
                logger.LogWarning(ex, "MoexBondUpdateJob: не удалось обработать SecId {SecId}", s);
            }
        });

        await using var writeCtx = await CreateDbContextAsync(ct);
        await writeCtx.BulkInsertOrUpdateAsync([.. entities], config =>
        {
            config.UpdateByProperties = [nameof(MoexBondEntity.SecId)];
            config.PropertiesToExcludeOnUpdate = [nameof(MoexBondEntity.Id)];
        }, cancellationToken: ct);
        await ReportProgressAsync(moexSecIds.Length, moexSecIds.Length, "Сохранение описаний облигаций", ct);
        if (!failedSecIds.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Не удалось синхронизировать описания облигаций MOEX для {failedSecIds.Distinct().Count()} бумаг.");
        }
    }

    private async Task<string[]> LoadSourceAsync(CancellationToken ct)
    {
        var payload = await moexHttpClient.GetActiveBondsSecuritiesAsync(ct);
        return payload.SelectMany(x => x.Value.Select(z => z.SecId)).Distinct().ToArray();
    }

    private static string NormalizeCurrency(string currencyId)
    {
        return currencyId == "SUR" ? "RUB" : currencyId;
    }
}

public interface IMoexBondUpdateJob : IHangfireJob
{
}
