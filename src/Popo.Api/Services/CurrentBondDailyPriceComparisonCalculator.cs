using Popo.Core.Common;
using Popo.Core.Contracts.Iss;
using Popo.Core.HttpClients;
using Popo.Core.Statistics;

namespace Popo.Api.Services;

public sealed class CurrentBondDailyPriceComparisonCalculator(
    IBondDailyPriceHistoryProvider historyProvider,
    IMoexHttpClient moexHttpClient)
    : IBondDailyPriceComparisonCalculator
{
    public async Task<BondDailyPriceComparison?> CalculateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var previousDate = await historyProvider.GetLatestTradingDateBeforeAsync(date, cancellationToken);
        if (previousDate is null)
        {
            return null;
        }

        var previousPrices = await historyProvider.GetClosingPricesAsync(previousDate.Value, cancellationToken);
        var marketdatas = (await moexHttpClient.GetActiveBondsMarketdataFreshAsync(cancellationToken)).Values
            .SelectMany(quotes => quotes)
            .ToArray();
        var securityMetadata = await historyProvider.GetSecurityMetadataAsync(
            marketdatas.Select(quote => quote.SecId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            cancellationToken);
        var currentPrices = marketdatas
            .Select(quote => ToLivePrice(
                quote,
                securityMetadata.GetValueOrDefault(quote.SecId)))
            .Where(price => price is not null)
            .Select(price => price!)
            .ToArray();

        return new BondDailyPriceComparison(previousDate.Value, previousPrices, currentPrices);
    }

    private static BondDailyPricePoint? ToLivePrice(MoexMarketdata quote, BondDailySecurityMetadata? metadata) =>
        !IsValidPrice(quote.CurrentPrice)
            ? null
            : new BondDailyPricePoint(
                quote.SecId,
                quote.BoardId,
                string.IsNullOrWhiteSpace(metadata?.ShortName) ? quote.SecId : metadata.ShortName,
                quote.CurrentPrice!.Value,
                quote.NumTrades,
                quote.ValueToday,
                RelationHelper.BoardCurrency.GetValueOrDefault(quote.BoardId),
                metadata?.IssuerCategory,
                metadata?.EmitterId);

    private static bool IsValidPrice(double? price) =>
        price is { } value && double.IsFinite(value) && value > 0;
}
