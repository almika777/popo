using Popo.Core.Bonds;
using Popo.Core.HttpClients;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Trades;

namespace Popo.Api.Services.Trades;

public sealed class PortfolioTradesService(
    IPortfolioTradesProvider tradesProvider,
    IBondsService bondsService,
    IMoexHttpClient moexHttpClient)
{
    public async Task<IReadOnlyList<PortfolioTradeRecord>> GetTradesAsync(CancellationToken cancellationToken)
    {
        var trades = await tradesProvider.GetTradesAsync(cancellationToken);
        if (trades.Count == 0)
            return trades;

        var securities = (await moexHttpClient.GetActiveBondsSecuritiesAsync(cancellationToken))
            .SelectMany(x => x.Value)
            .ToArray();

        return trades.Select(trade => trade with
        {
            ShortName = securities.FirstOrDefault(x => x.SecId == trade.SecId && x.BoardId == trade.BoardId)?.ShortName
                ?? trade.SecId
        }).ToArray();
    }

    public async Task<PortfolioTradeRecord> AddTradeAsync(
        PortfolioTrade trade,
        CancellationToken cancellationToken)
    {
        var tradeWithHistoricalFaceValue = await UseFaceValueAtTradeDateAsync(trade, cancellationToken);
        return await tradesProvider.AddTradeAsync(tradeWithHistoricalFaceValue, cancellationToken);
    }

    public async Task<bool> UpdateTradeAsync(
        Guid id,
        PortfolioTrade trade,
        CancellationToken cancellationToken)
    {
        var tradeWithHistoricalFaceValue = await UseFaceValueAtTradeDateAsync(trade, cancellationToken);
        return await tradesProvider.UpdateTradeAsync(id, tradeWithHistoricalFaceValue, cancellationToken);
    }

    public Task<bool> DeleteTradeAsync(Guid id, CancellationToken cancellationToken) =>
        tradesProvider.DeleteTradeAsync(id, cancellationToken);

    private async Task<PortfolioTrade> UseFaceValueAtTradeDateAsync(
        PortfolioTrade trade,
        CancellationToken cancellationToken)
    {
        var result = (await bondsService.GetHistoricalFaceValuesAsync(
            [new BondFaceValueRequest(trade.SecId, trade.BoardId, trade.TradeDate)],
            cancellationToken)).FirstOrDefault();
        if (result?.FaceValue is not { } historicalFaceValue
            || !double.IsFinite(historicalFaceValue)
            || historicalFaceValue <= 0)
        {
            return trade;
        }

        return trade with { FaceValue = historicalFaceValue };
    }
}
