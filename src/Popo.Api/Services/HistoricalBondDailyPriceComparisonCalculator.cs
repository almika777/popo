using Popo.Core.Statistics;

namespace Popo.Api.Services;

public sealed class HistoricalBondDailyPriceComparisonCalculator(IBondDailyPriceHistoryProvider historyProvider)
    : IBondDailyPriceComparisonCalculator
{
    public async Task<BondDailyPriceComparison?> CalculateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var currentPrices = await historyProvider.GetClosingPricesAsync(date, cancellationToken);
        if (currentPrices.Count == 0)
            return null;
        
        var previousDate = await historyProvider.GetLatestTradingDateBeforeAsync(date, cancellationToken);
        if (previousDate is null)
            return null;
        
        var previousPrices = await historyProvider.GetClosingPricesAsync(previousDate.Value, cancellationToken);
        return new BondDailyPriceComparison(previousDate.Value, previousPrices, currentPrices);
    }
}
