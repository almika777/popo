using Popo.Api.Models;
using Popo.Core;
using Popo.Core.Bonds;
using Popo.Core.Common;
using Popo.Core.Statistics;
using Popo.HttpClients;

namespace Popo.Api.Services.Statistics;

public class StatisticsTurnoversService(
    TimeProvider timeProvider,
    IHistoryProvider historyProvider,
    IBondsProvider bondsProvider,
    MoexIssClient moexIssClient)
{
    public async Task<IReadOnlyList<BondDailyTurnover>> Get(
        BondDailyStatisticsRequest request,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(MoscowTime.ToMoscow(timeProvider.GetUtcNow()).DateTime);

        return request.Date == today
            ? await GetToday(request, ct)
            : await GetPrevious(request, ct);
    }

    private async Task<IReadOnlyList<BondDailyTurnover>> GetToday(
        BondDailyStatisticsRequest request,
        CancellationToken ct)
    {
        var marketdata = (await moexIssClient.GetBondsMarketdataAsync(ct)).Values.SelectMany(x => x);
        var securities = await bondsProvider.GetSecuritiesAsync(ct);
        var result = new List<BondDailyTurnover>();

        foreach (var current in marketdata
                     .Where(x => x.NumTrades >= request.MinimumTrades &&
                                 x.ValueToday >= request.MinimumTurnoverRub))
        {
            if (current.ValueToday is not { } turnover)
                continue;

            var security = securities.FirstOrDefault(x => x.SecId == current.SecId);
            if (security.SecId == null)
                continue;

            result.Add(new BondDailyTurnover(
                current.SecId,
                current.BoardId,
                security.ShortName!,
                turnover,
                security.EmitterId));
        }

        return result.OrderByDescending(x => x.TurnoverRub).Take(5).ToList();
    }

    private async Task<IReadOnlyList<BondDailyTurnover>> GetPrevious(
        BondDailyStatisticsRequest request,
        CancellationToken ct)
    {
        var requested = await historyProvider.GetStatisticsAsync(request.Date, ct);
        var securities = await bondsProvider.GetSecuritiesAsync(ct);
        var result = new List<BondDailyTurnover>();

        foreach (var current in requested
                     .Where(x => x.Value.NumTrades >= request.MinimumTrades &&
                                 x.Value.Value >= request.MinimumTurnoverRub))
        {
            if (current.Value.Value is not { } turnover)
                continue;

            var security = securities.FirstOrDefault(x => x.SecId == current.Key.SecId);
            if (security.SecId == null)
                continue;

            result.Add(new BondDailyTurnover(
                current.Key.SecId,
                current.Key.BoardId,
                security.ShortName!,
                turnover,
                security.EmitterId));
        }

        return result.OrderByDescending(x => x.TurnoverRub).Take(5).ToList();
    }
}
