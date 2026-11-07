using Popo.Api.Models;
using Popo.Core;
using Popo.Core.Statistics;

namespace Popo.Api.Services.Statistics;

public class BondsStatisticsService(
    IHistoryProvider historyProvider,
    StatisticsIncreasesService statisticsIncreasesService,
    StatisticsDeclinesService statisticsDeclinesService,
    StatisticsTurnoversService statisticsTurnoversService)
{
    public async Task<BondDailyStatisticsResult> GetDailyStatisticsAsync(
        BondDailyStatisticsRequest request,
        CancellationToken ct)
    {
        var previousDate = await historyProvider.GetLatestTradingDateBeforeAsync(request.Date, ct);

        if (previousDate is null)
            return new BondDailyStatisticsResult(request.Date, null, [], [], []);
        
        var turnoversTask = statisticsTurnoversService.Get(request, ct);
        var increasesTask = statisticsIncreasesService.Get(request, previousDate.Value, ct);
        var declinesTask = statisticsDeclinesService.Get(request, previousDate.Value, ct);

        await Task.WhenAll(turnoversTask, increasesTask, declinesTask);
        var increases = await increasesTask;
        var declines = await declinesTask;
        return new BondDailyStatisticsResult(
            request.Date,
            increases.HasComparablePrices || declines.HasComparablePrices ? previousDate.Value : null,
            declines.Changes,
            increases.Changes,
            await turnoversTask);
    }
}
