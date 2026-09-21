using Microsoft.EntityFrameworkCore;
using Popo.Core;

namespace Popo.Storage.Providers;

public class HistoryProvider(IDbContextFactory<PopoDbContext> dbContextFactory) : IHistoryProvider
{
    public async Task<Dictionary<string, double>> GetMedianDailyVolume(CancellationToken ct)
        => (await GetDailyVolumeStatistics(ct)).ToDictionary(x => x.Key, x => x.Value.Median);

    public async Task<Dictionary<string, DailyVolumeStatistics>> GetDailyVolumeStatistics(CancellationToken ct)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        var statistics = await dbContext.DailyVolumeStatistics.AsNoTracking().ToListAsync(ct);

        return statistics.ToDictionary(x => x.SecId, x => new DailyVolumeStatistics(x.Average, x.Median));
    }

}
