using System.Collections.Concurrent;
using EFCore.BulkExtensions;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Popo.Core.Common;
using Popo.Core.HttpClients;
using Popo.Jobs.Initialization;
using Popo.Storage;
using Popo.Storage.Entities.Moex;

namespace Popo.Jobs.Jobs;

public sealed class MoexAmortsAndCouponsUpdateJob(
    IMoexHttpClient moexHttpClient,
    IDbContextFactory<PopoDbContext> dbContextFactory,
    ILogger<MoexAmortsAndCouponsUpdateJob> logger,
    IInitializationProgressReporter progressReporter)
    : JobBase(dbContextFactory, logger, progressReporter), IMoexAmortsAndCouponsUpdateJob
{
    public async Task UpdateAsync(CancellationToken ct = default)
    {
        await using var initContext = await CreateDbContextAsync(ct);

        var allowedBoards = RelationHelper.BoardCurrency.Keys.ToList();
        var activeBonds = await initContext.ActiveSecEntities
            .Where(x => allowedBoards.Contains(x.PrimaryBoardId))
            .Select(x => x.SecId)
            .ToListAsync(ct);
        await ReportProgressAsync(0, activeBonds.Count, "Загрузка купонов и амортизаций", ct);

        var amorts = new ConcurrentBag<MoexAmortsEntity>();
        var coupons = new ConcurrentBag<MoexCouponsEntity>();
        var failedSecIds = new ConcurrentBag<string>();
        var processedBonds = 0;
        await Parallel.ForEachAsync(activeBonds, new ParallelOptions()
        {
            MaxDegreeOfParallelism = 5
        }, async (x, ctt) =>
        {
            try
            {
                var bondization = await moexHttpClient.GetFutureAmortsAndCoupons(x, ctt);

                bondization.Amortizations.ForEach(z => amorts.Add(z.Adapt<MoexAmortsEntity>()));
                bondization.Coupons.ForEach(z => coupons.Add(z.Adapt<MoexCouponsEntity>()));
            }
            catch (Exception ex)
            {
                failedSecIds.Add(x);
                logger.LogError(ex, "Ошибка при обработке SecId {SecId}", x);
            }
            finally
            {
                var processed = Interlocked.Increment(ref processedBonds);
                if (processed % 10 == 0 || processed == activeBonds.Count)
                    await ReportProgressAsync(processed, activeBonds.Count, "Загрузка купонов и амортизаций", ctt);
            }
        });

        var amortsList = amorts.ToList();
        var couponsList = coupons.ToList();

        foreach (var chunk in amortsList.Chunk(BatchSize))
        {
            await SaveAmortsBatchAsync(chunk, ct);
        }

        foreach (var chunk in couponsList.Chunk(BatchSize))
        {
            await SaveCouponsBatchAsync(chunk, ct);
        }
        await ReportProgressAsync(activeBonds.Count, activeBonds.Count, "Сохранение купонов и амортизаций", ct);
        if (!failedSecIds.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Не удалось синхронизировать купоны и амортизации MOEX для {failedSecIds.Distinct().Count()} бумаг.");
        }
    }

    private async Task SaveAmortsBatchAsync(MoexAmortsEntity[] batch, CancellationToken ct)
    {
        try
        {
            await using var context = await CreateDbContextAsync(ct);
            await context.BulkInsertOrUpdateAsync(batch, new BulkConfig
            {
                UpdateByProperties = [nameof(MoexAmortsEntity.SecId), nameof(MoexAmortsEntity.AmortDate)],
                PropertiesToExcludeOnUpdate = [nameof(MoexAmortsEntity.SecId), nameof(MoexAmortsEntity.AmortDate)]
            }, cancellationToken: ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Ошибка при сохранении пачки амортизаций");
            throw;
        }
    }

    private async Task SaveCouponsBatchAsync(MoexCouponsEntity[] batch, CancellationToken ct)
    {
        try
        {
            await using var context = await CreateDbContextAsync(ct);
            await context.BulkInsertOrUpdateAsync(batch, new BulkConfig
            {
                UpdateByProperties = [nameof(MoexCouponsEntity.SecId), nameof(MoexCouponsEntity.CouponDate)],
                PropertiesToExcludeOnUpdate = [nameof(MoexCouponsEntity.SecId), nameof(MoexCouponsEntity.CouponDate)]
            }, cancellationToken: ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Ошибка при сохранении пачки купонов");
            throw;
        }
    }
}

public interface IMoexAmortsAndCouponsUpdateJob : IHangfireJob
{
}
