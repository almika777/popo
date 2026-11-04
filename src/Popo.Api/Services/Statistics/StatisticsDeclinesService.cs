using Popo.Api.Models;
using Popo.Core;
using Popo.Core.Bonds;
using Popo.Core.Common;
using Popo.Core.Statistics;
using Popo.HttpClients;

namespace Popo.Api.Services.Statistics;

public class StatisticsDeclinesService(
    TimeProvider timeProvider,
    IHistoryProvider historyProvider,
    IBondsProvider bondsProvider,
    MoexIssClient moexIssClient)
{
    public async Task<IReadOnlyList<BondDailyChange>> Get(
        BondDailyStatisticsRequest request,
        DateOnly previousDate,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(MoscowTime.ToMoscow(timeProvider.GetUtcNow()).DateTime);

        return request.Date == today
            ? await GetToday(request, previousDate, ct)
            : await GetPrevious(request, previousDate, ct);
    }

    private async Task<IReadOnlyList<BondDailyChange>> GetToday(
        BondDailyStatisticsRequest request,
        DateOnly previousDate,
        CancellationToken ct)
    {
        var marketdata = (await moexIssClient.GetBondsMarketdataAsync(ct)).Values.SelectMany(x => x);
        var previousStatistics = await historyProvider.GetStatisticsAsync(previousDate, ct);
        var securities = await bondsProvider.GetSecuritiesAsync(ct);

        var result = new List<BondDailyChange>();
        foreach (var current in marketdata
                     .Where(x => x.NumTrades >= request.MinimumTrades &&
                                 x.ValueToday >= request.MinimumTurnoverRub))
        {
            var security = securities.FirstOrDefault(x => x.SecId == current.SecId);
            if (security.SecId == null)
                continue;

            if (!previousStatistics.TryGetValue(new SecBoard(current.SecId, current.BoardId), out var previous))
                continue;

            if (current.Last is not { } currentPrice ||
                current.LastToPrevPrice is not { } changePercent ||
                previous.ClosePrice is not { } previousPrice ||
                !IsValidPrice(currentPrice) ||
                !double.IsFinite(changePercent))
                continue;

            var changePoints = currentPrice - previousPrice;

            result.Add(new BondDailyChange(
                current.SecId,
                current.BoardId,
                security.ShortName!,
                previousPrice,
                currentPrice,
                changePoints,
                changePercent,
                security.EmitterId));
        }

        return result.OrderBy(x => x.ChangePercent).Take(5).ToList();
    }

    private async Task<IReadOnlyList<BondDailyChange>> GetPrevious(
        BondDailyStatisticsRequest request,
        DateOnly previousDate,
        CancellationToken ct)
    {
        var requested = await historyProvider.GetStatisticsAsync(request.Date, ct);
        var dayBefore = await historyProvider.GetStatisticsAsync(previousDate, ct);
        var securities = await bondsProvider.GetSecuritiesAsync(ct);

        var result = new List<BondDailyChange>();
        foreach (var current in requested
                     .Where(x => x.Value.NumTrades >= request.MinimumTrades &&
                                 x.Value.Value >= request.MinimumTurnoverRub))
        {
            var security = securities.FirstOrDefault(x => x.SecId == current.Key.SecId);
            if (security.SecId == null)
                continue;

            if (!dayBefore.TryGetValue(current.Key, out var previous))
                continue;

            if (current.Value.ClosePrice is not { } currentPrice ||
                previous.ClosePrice is not { } previousPrice ||
                !IsValidPrice(currentPrice) ||
                !IsValidPrice(previousPrice))
                continue;

            var changePoints = currentPrice - previousPrice;
            var changePercent = changePoints / previousPrice * 100;

            result.Add(new BondDailyChange(
                current.Key.SecId,
                current.Key.BoardId,
                security.ShortName!,
                previousPrice,
                currentPrice,
                changePoints,
                changePercent,
                security.EmitterId));
        }

        return result.OrderBy(x => x.ChangePercent).Take(5).ToList();
    }

    private static bool IsValidPrice(double price) => double.IsFinite(price) && price > 0;
}
