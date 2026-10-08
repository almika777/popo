using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Popo.Core.HttpClients;
using Popo.Core.PortfolioReturns;
using Popo.Jobs.Initialization;
using Popo.Storage;

namespace Popo.Jobs.Jobs;

public sealed class MoexIndexHistoryUpdateJob(
    IMoexHttpClient moexHttpClient,
    IMoexIndexHistoryStore indexHistoryStore,
    IDbContextFactory<PopoDbContext> dbContextFactory,
    ILogger<MoexIndexHistoryUpdateJob> logger,
    IInitializationProgressReporter progressReporter)
    : JobBase(dbContextFactory, logger, progressReporter), IMoexIndexHistoryUpdateJob
{
    public async Task UpdateAsync(CancellationToken ct = default)
    {
        await ReportProgressAsync(0, 1, "Загрузка истории LQDT", ct);

        var history = await moexHttpClient.GetLqdtIndexHistoryAsync(ct);
        if (history.Count == 0)
        {
            throw new InvalidOperationException("MOEX не вернул историю индекса LQDTM.");
        }

        if (history.Any(x => !string.Equals(x.SecId, "LQDTM", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("MOEX вернул историю не того индекса для загрузки LQDTM.");
        }

        await indexHistoryStore.UpsertAsync(history, ct);
        await ReportProgressAsync(1, 1, "История LQDT сохранена", ct);
        logger.LogInformation("Сохранено {Count} дневных значений индекса LQDTM.", history.Count);
    }
}

public interface IMoexIndexHistoryUpdateJob : IHangfireJob;
