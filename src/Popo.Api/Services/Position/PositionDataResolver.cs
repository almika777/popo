using Popo.Core.Common;
using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;

namespace Popo.Api.Services.Position;

internal static class PositionDataResolver
{
    internal static IReadOnlyList<PositionData> ResolvePositionData(PositionSourceData sourceData)
    {
        var marketdataByKey = sourceData.Marketdata
            .GroupBy(x => InstrumentKey(x.SecId, x.BoardId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var marketdataBySecId = sourceData.Marketdata
            .GroupBy(x => x.SecId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);
        var securitiesByKey = sourceData.Securities
            .GroupBy(x => InstrumentKey(x.SecId, x.BoardId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        return sourceData.Positions.Select(position =>
        {
            var marketdata = GetMarketdataValue(marketdataByKey, marketdataBySecId, position);
            var securityKey = marketdata is not null
                ? InstrumentKey(marketdata.SecId, marketdata.BoardId)
                : InstrumentKey(position.SecId, position.BoardId);
            var security = securitiesByKey.GetValueOrDefault(securityKey);
            return new PositionData(
                position,
                marketdata,
                security,
                sourceData.VolumeStatistics.GetValueOrDefault(position.SecId));
        }).ToArray();
    }

    private static MoexMarketdata? GetMarketdataValue(
        Dictionary<string, MoexMarketdata> marketdataByKey,
        Dictionary<string, MoexMarketdata[]> marketdataBySecId,
        PortfolioPositionRecord position)
    {
        var marketdataValue = marketdataByKey
            .GetValueOrDefault(InstrumentKey(position.SecId, position.BoardId));

        if (marketdataValue is null && marketdataBySecId.TryGetValue(position.SecId, out var quotes))
        {
            marketdataValue = quotes.FirstOrDefault(x =>
                RelationHelper.BoardCurrency.TryGetValue(x.BoardId, out var currency)
                && string.Equals(currency, position.CurrencyId, StringComparison.OrdinalIgnoreCase));
        }

        return marketdataValue;
    }

    private static string InstrumentKey(string secId, string boardId) => $"{secId}\u001F{boardId}";

}
