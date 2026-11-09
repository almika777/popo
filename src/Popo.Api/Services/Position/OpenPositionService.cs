using Popo.Api.Models;
using Popo.Core;
using Popo.Core.HttpClients;
using Popo.Core.Portfolio.Trades;

namespace Popo.Api.Services.Position;

public class OpenPositionService(
    IPortfolioTradesProvider portfolioTradesProvider,
    IMoexHttpClient moexHttpClient,
    IPositionBuilderFactory positionBuilderFactory) : IOpenPositionsService
{
    public async Task<PositionsPageResponse> GetAsync(CancellationToken cancellationToken)
    {
        var trades = await portfolioTradesProvider.GetTradesAsync(cancellationToken);
        var securities = (await moexHttpClient.GetActiveBondsSecuritiesAsync(cancellationToken))
            .SelectMany(x => x.Value)
            .ToDictionary(x => new SecBoard(x.SecId, x.BoardId));
        
        var marketdatas = (await moexHttpClient.GetActiveBondsMarketdataAsync(cancellationToken))
            .SelectMany(x => x.Value)
            .ToDictionary(x => new SecBoard(x.SecId, x.BoardId));
        
        var res = new List<PositionResponse>();
        foreach (var portfolioTradeRecords in trades.GroupBy(x => new SecBoard(x.SecId, x.BoardId)))
        {
            var security = securities[portfolioTradeRecords.Key];
            var marketdata = marketdatas[portfolioTradeRecords.Key];
            
            var position = await positionBuilderFactory
                .Get(security.BondType)
                .Build(portfolioTradeRecords.ToList(), marketdata);
            
            res.Add(position);
        }
        
        return new PositionsPageResponse(res, new PositionTotalsResponse(1, 1, 1, 1));
    }
}