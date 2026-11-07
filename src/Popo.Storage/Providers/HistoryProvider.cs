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

        var statistics = await dbContext.DailyVolumeStatistics.ToListAsync(ct);
        return statistics.ToDictionary(x => x.SecId, x => new DailyVolumeStatistics(x.Average, x.Median));
    }

    public async Task<DateOnly?> GetLatestTradingDateBeforeAsync(DateOnly date, CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.MoexHistoryYieldsEntities
            .AsNoTracking()
            .Where(price =>
                price.TradeDate < date && price.Close > 0 && price.Close <= double.MaxValue)
            .OrderByDescending(price => price.TradeDate)
            .Select(price => (DateOnly?)price.TradeDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IDictionary<SecBoard, DateStatistics>> GetStatisticsAsync(DateOnly date, CancellationToken ct)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        return await context.MoexHistoryYieldsEntities
            .AsNoTracking()
            .Where(x => x.TradeDate == date)
            .ToDictionaryAsync(
                x => new SecBoard(x.SecId, x.BoardId), 
                x => new DateStatistics(x.Close, x.NumTrades, x.Value), cancellationToken: ct);
    }
}
