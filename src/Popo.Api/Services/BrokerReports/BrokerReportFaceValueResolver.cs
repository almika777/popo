using Popo.Core.Bonds;
using Popo.Core.Common;

namespace Popo.Api.Services.BrokerReports;

public sealed class BrokerReportFaceValueResolver(IBondsService bondsService)
{
    public async Task<IReadOnlyDictionary<int, double?>> ResolveAsync(
        IReadOnlyList<ParsedBrokerTrade> trades,
        IReadOnlyDictionary<string, BondSearchResult> bondDetails,
        CancellationToken cancellationToken)
    {
        var requests = new List<(int TradeIndex, BondFaceValueRequest Request)>();
        for (var index = 0; index < trades.Count; index++)
        {
            var trade = trades[index];
            if (RelationHelper.MoneyMarketFunds.ContainsKey(trade.InstrumentCode))
                continue;

            var boardId = string.IsNullOrWhiteSpace(trade.TradingMode)
                ? bondDetails.GetValueOrDefault(trade.InstrumentCode)?.BoardId
                : trade.TradingMode;
            if (string.IsNullOrWhiteSpace(boardId))
                continue;

            requests.Add((index, new BondFaceValueRequest(
                trade.InstrumentCode, boardId, trade.TradeDate)));
        }

        if (requests.Count == 0)
            return new Dictionary<int, double?>();

        var results = await bondsService.GetHistoricalFaceValuesAsync(
            requests.Select(x => x.Request).ToArray(), cancellationToken);
        var faceValuesByTradeIndex = new Dictionary<int, double?>();
        for (var index = 0; index < requests.Count && index < results.Count; index++)
        {
            var faceValue = results[index].FaceValue;
            faceValuesByTradeIndex[requests[index].TradeIndex] =
                faceValue is { } value && double.IsFinite(value) && value > 0 ? value : null;
        }

        return faceValuesByTradeIndex;
    }
}
