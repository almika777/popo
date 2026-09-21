using System.Collections.Concurrent;
using System.Threading.Channels;
using EFCore.BulkExtensions;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Popo.Core.Common;
using Popo.Core.HttpClients;
using Popo.Jobs.Initialization;
using Popo.Storage;
using Popo.Storage.Entities.Moex;
using Popo.Storage.Providers;

namespace Popo.Jobs.Jobs;

public sealed class MoexHistoryPricesUpdateJob(
    IMoexHttpClient moexHttpClient,
    IDbContextFactory<PopoDbContext> dbContextFactory,
    ILogger<MoexHistoryPricesUpdateJob> logger,
    DailyVolumeStatisticsStore statisticsStore,
    IInitializationProgressReporter progressReporter)
    : JobBase(dbContextFactory, logger, progressReporter), IMoexHistoryPricesUpdateJob
{
    private readonly IDbContextFactory<PopoDbContext> _dbContextFactory = dbContextFactory;
    private new const int BatchSize = 2000;

    public async Task UpdateAsync(CancellationToken ct = default)
    {
        await using var initContext = await _dbContextFactory.CreateDbContextAsync(ct);

        var activeBonds = (await initContext.ActiveSecEntities.ToListAsync(ct))
            .Where(x => RelationHelper.BoardCurrency.ContainsKey(x.PrimaryBoardId)).ToList();
        await ReportProgressAsync(0, activeBonds.Count, "Загрузка истории цен", ct);

        var maxDatesBySecIdBoardId = await initContext.MoexHistoryYieldsEntities
            .GroupBy(x => new { x.SecId, x.BoardId })
            .Select(g => new { g.Key.SecId, g.Key.BoardId, MaxDate = g.Max(z => z.TradeDate) })
            .ToDictionaryAsync(x => (x.SecId, x.BoardId), x => x.MaxDate, ct);

        // Канал разгружает пул подключений: пишет строго один поток
        var channel = Channel.CreateBounded<MoexHistoryYieldsEntity>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        await using var writeContext = await _dbContextFactory.CreateDbContextAsync(ct);

        // Потребитель: забирает данные из канала и пишет в Postgres через COPY
        var dbWriterTask = Task.Run(async () =>
        {
            var batch = new List<MoexHistoryYieldsEntity>(BatchSize);

            try
            {
                await foreach (var item in channel.Reader.ReadAllAsync(ct))
                {
                    batch.Add(item);

                    if (batch.Count >= BatchSize)
                    {
                        await writeContext.BulkInsertAsync(batch, cancellationToken: ct);
                        batch.Clear();
                    }
                }

                if (batch.Count > 0)
                {
                    await writeContext.BulkInsertAsync(batch, cancellationToken: ct);
                }
            }
            catch (Exception exception)
            {
                channel.Writer.TryComplete(exception);
                throw;
            }
        }, ct);

        var yesterday = MoscowTime.Today.AddDays(-1);
        var failedSecIds = new ConcurrentBag<string>();
        var processedBonds = 0;

        try
        {
            await Parallel.ForEachAsync(activeBonds, new ParallelOptions { MaxDegreeOfParallelism = 2 },
                async (bond, token) =>
                {
                    try
                    {
                        if (maxDatesBySecIdBoardId.TryGetValue((bond.SecId, bond.PrimaryBoardId), out var maxDate) &&
                            maxDate == yesterday)
                            return;

                        var from = maxDate == default ? new DateOnly(2025, 01, 01) : maxDate.AddDays(1);

                        var resBySec = await moexHttpClient
                            .GetBondHistoryPageAsync(bond.SecId, bond.PrimaryBoardId, from: from, ct: token);

                        if (resBySec.Count == 0)
                            return;

                        foreach (var item in resBySec)
                        {
                            await channel.Writer.WriteAsync(item.Adapt<MoexHistoryYieldsEntity>(), token);
                        }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        failedSecIds.Add(bond.SecId);
                        logger.LogError(ex, "Ошибка при обработке SecId {SecId}", bond.SecId);
                    }
                    finally
                    {
                        var processed = Interlocked.Increment(ref processedBonds);
                        if (processed % 10 == 0 || processed == activeBonds.Count)
                            await ReportProgressAsync(processed, activeBonds.Count, "Загрузка истории цен", token);
                    }
                });
        }
        catch (Exception exception)
        {
            channel.Writer.TryComplete(exception);
            try
            {
                await dbWriterTask;
            }
            catch
            {
                // Preserve the original processing failure.
            }

            throw;
        }

        channel.Writer.TryComplete();
        await dbWriterTask;
        if (!failedSecIds.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Не удалось синхронизировать историю MOEX для {failedSecIds.Distinct().Count()} бумаг.");
        }

        await statisticsStore.RefreshAsync(ct);
        await ReportProgressAsync(activeBonds.Count, activeBonds.Count, "Обновление статистики объёмов", ct);
        logger.LogInformation("Статистика дневных объёмов обновлена после синхронизации истории.");
    }
}

public interface IMoexHistoryPricesUpdateJob : IHangfireJob
{
}
