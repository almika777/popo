using Popo.Api.Models;
using Popo.Core;
using Popo.Core.Bonds;
using Popo.Core.Common;
using Popo.Core.Portfolio;
using Popo.Core.Statistics;
using Popo.HttpClients;

namespace Popo.Api.Services.Statistics;

public class StatisticsDeclinesService(
    TimeProvider timeProvider,
    IHistoryProvider historyProvider,
    IBondsProvider bondsProvider,
    MoexIssClient moexIssClient,
    IPortfolioCurrencyRatesProvider currencyRatesProvider)
{
    public async Task<(IReadOnlyList<BondDailyChange> Changes, bool HasComparablePrices)> Get(
        BondDailyStatisticsRequest request,
        DateOnly previousDate,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(MoscowTime.ToMoscow(timeProvider.GetUtcNow()).DateTime);

        return request.Date == today
            ? await GetToday(request, previousDate, ct)
            : await GetPrevious(request, previousDate, ct);
    }

    private async Task<(IReadOnlyList<BondDailyChange> Changes, bool HasComparablePrices)> GetToday(
        BondDailyStatisticsRequest request,
        DateOnly previousDate,
        CancellationToken ct)
    {
        var marketdata = (await moexIssClient.GetBondsMarketdataAsync(ct)).Values.SelectMany(x => x);
        var previousStatistics = await historyProvider.GetStatisticsAsync(previousDate, ct);
        var securities = await bondsProvider.GetSecuritiesAsync(ct);
        var securitiesByBoard = securities.ToDictionary(security => (security.SecId, security.BoardId));
        var candidates = marketdata
            .Where(quote => quote.NumTrades >= request.MinimumTrades)
            .Select(quote =>
            {
                securitiesByBoard.TryGetValue((quote.SecId, quote.BoardId), out var security);
                return (Quote: quote, Security: security,
                    CurrencyId: BondDailyTurnoverRubConverter.GetCurrencyId(security.CurrencyId, quote.BoardId));
            })
            .Where(candidate => candidate.Security.SecId is not null
                && (request.IssuerCategory is null || candidate.Security.IssuerCategory == request.IssuerCategory))
            .ToArray();
        var ratesToRub = await BondDailyTurnoverRubConverter.GetRatesToRubAsync(
            currencyRatesProvider, candidates.Select(candidate => candidate.CurrencyId), request.Date, ct);

        var result = new List<BondDailyChange>();
        var hasComparablePrices = false;
        foreach (var candidate in candidates)
        {
            var current = candidate.Quote;
            var security = candidate.Security!;
            var turnoverRub = BondDailyTurnoverRubConverter.ConvertToRub(current.ValueToday, candidate.CurrencyId, ratesToRub);
            if (turnoverRub is null || turnoverRub < request.MinimumTurnoverRub)
                continue;

            if (!previousStatistics.TryGetValue(new SecBoard(current.SecId, current.BoardId), out var previous))
                continue;

            if (current.Last is not { } currentPrice ||
                previous.ClosePrice is not { } previousPrice ||
                !IsValidPrice(currentPrice) ||
                !IsValidPrice(previousPrice))
                continue;

            var changePoints = currentPrice - previousPrice;
            var changePercent = changePoints / previousPrice * 100;
            if (!double.IsFinite(changePercent))
                continue;

            hasComparablePrices = true;
            if (changePercent >= 0)
                continue;

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

        return (result.OrderBy(x => x.ChangePercent).Take(5).ToList(), hasComparablePrices);
    }

    private async Task<(IReadOnlyList<BondDailyChange> Changes, bool HasComparablePrices)> GetPrevious(
        BondDailyStatisticsRequest request,
        DateOnly previousDate,
        CancellationToken ct)
    {
        var requested = await historyProvider.GetStatisticsAsync(request.Date, ct);
        var dayBefore = await historyProvider.GetStatisticsAsync(previousDate, ct);
        var securities = await bondsProvider.GetSecuritiesAsync(ct);
        var securitiesByBoard = securities.ToDictionary(security => (security.SecId, security.BoardId));
        var candidates = requested
            .Where(entry => entry.Value.NumTrades >= request.MinimumTrades)
            .Select(entry =>
            {
                securitiesByBoard.TryGetValue((entry.Key.SecId, entry.Key.BoardId), out var security);
                return (Entry: entry, Security: security,
                    CurrencyId: BondDailyTurnoverRubConverter.GetCurrencyId(security.CurrencyId, entry.Key.BoardId));
            })
            .Where(candidate => candidate.Security.SecId is not null
                && (request.IssuerCategory is null || candidate.Security.IssuerCategory == request.IssuerCategory))
            .ToArray();
        var ratesToRub = await BondDailyTurnoverRubConverter.GetRatesToRubAsync(
            currencyRatesProvider, candidates.Select(candidate => candidate.CurrencyId), request.Date, ct);

        var result = new List<BondDailyChange>();
        var hasComparablePrices = false;
        foreach (var candidate in candidates)
        {
            var current = candidate.Entry;
            var security = candidate.Security!;
            var turnoverRub = BondDailyTurnoverRubConverter.ConvertToRub(current.Value.Value, candidate.CurrencyId, ratesToRub);
            if (turnoverRub is null || turnoverRub < request.MinimumTurnoverRub)
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
            if (!double.IsFinite(changePercent))
                continue;

            hasComparablePrices = true;
            if (changePercent >= 0)
                continue;

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

        return (result.OrderBy(x => x.ChangePercent).Take(5).ToList(), hasComparablePrices);
    }

    private static bool IsValidPrice(double price) => double.IsFinite(price) && price > 0;
}
