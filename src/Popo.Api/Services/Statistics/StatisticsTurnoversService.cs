using Popo.Api.Models;
using Popo.Core;
using Popo.Core.Bonds;
using Popo.Core.Common;
using Popo.Core.Portfolio;
using Popo.Core.Statistics;
using Popo.HttpClients;

namespace Popo.Api.Services.Statistics;

public class StatisticsTurnoversService(
    TimeProvider timeProvider,
    IHistoryProvider historyProvider,
    IBondsProvider bondsProvider,
    MoexIssClient moexIssClient,
    IPortfolioCurrencyRatesProvider currencyRatesProvider)
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
        var securitiesByBoard = securities.ToDictionary(security => (security.SecId, security.BoardId));
        var candidates = marketdata
            .Where(quote => quote.NumTrades >= request.MinimumTrades && quote.ValueToday is > 0)
            .Select(quote =>
            {
                securitiesByBoard.TryGetValue((quote.SecId, quote.BoardId), out var security);
                return (Quote: quote, Security: security, CurrencyId: BondDailyTurnoverRubConverter.GetCurrencyId(security.CurrencyId, quote.BoardId));
            })
            .Where(candidate => candidate.Security.SecId is not null
                && (request.IssuerCategory is null || candidate.Security.IssuerCategory == request.IssuerCategory))
            .ToArray();
        var ratesToRub = await BondDailyTurnoverRubConverter.GetRatesToRubAsync(
            currencyRatesProvider,
            candidates.Select(candidate => candidate.CurrencyId),
            request.Date,
            ct);
        var result = new List<BondDailyTurnover>(candidates.Length);

        foreach (var candidate in candidates)
        {
            var turnoverRub = BondDailyTurnoverRubConverter.ConvertToRub(candidate.Quote.ValueToday, candidate.CurrencyId, ratesToRub);
            if (turnoverRub is null || turnoverRub < request.MinimumTurnoverRub)
                continue;

            result.Add(new BondDailyTurnover(
                candidate.Quote.SecId,
                candidate.Quote.BoardId,
                candidate.Security.ShortName!,
                turnoverRub.Value,
                candidate.Security.EmitterId));
        }

        return result.OrderByDescending(x => x.TurnoverRub).Take(5).ToList();
    }

    private async Task<IReadOnlyList<BondDailyTurnover>> GetPrevious(
        BondDailyStatisticsRequest request,
        CancellationToken ct)
    {
        var requested = await historyProvider.GetStatisticsAsync(request.Date, ct);
        var securities = await bondsProvider.GetSecuritiesAsync(ct);
        var securitiesByBoard = securities.ToDictionary(security => (security.SecId, security.BoardId));
        var candidates = requested
            .Where(entry => entry.Value.NumTrades >= request.MinimumTrades && entry.Value.Value is > 0)
            .Select(entry =>
            {
                securitiesByBoard.TryGetValue((entry.Key.SecId, entry.Key.BoardId), out var security);
                return (Entry: entry, Security: security, CurrencyId: BondDailyTurnoverRubConverter.GetCurrencyId(security.CurrencyId, entry.Key.BoardId));
            })
            .Where(candidate => candidate.Security.SecId is not null
                && (request.IssuerCategory is null || candidate.Security.IssuerCategory == request.IssuerCategory))
            .ToArray();
        var ratesToRub = await BondDailyTurnoverRubConverter.GetRatesToRubAsync(
            currencyRatesProvider,
            candidates.Select(candidate => candidate.CurrencyId),
            request.Date,
            ct);
        var result = new List<BondDailyTurnover>(candidates.Length);

        foreach (var candidate in candidates)
        {
            var turnoverRub = BondDailyTurnoverRubConverter.ConvertToRub(candidate.Entry.Value.Value, candidate.CurrencyId, ratesToRub);
            if (turnoverRub is null || turnoverRub < request.MinimumTurnoverRub)
                continue;

            result.Add(new BondDailyTurnover(
                candidate.Entry.Key.SecId,
                candidate.Entry.Key.BoardId,
                candidate.Security.ShortName!,
                turnoverRub.Value,
                candidate.Security.EmitterId));
        }

        return result.OrderByDescending(x => x.TurnoverRub).Take(5).ToList();
    }

}
