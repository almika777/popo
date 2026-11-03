using Popo.Core.Common;
using Popo.Core.Portfolio;
using Popo.Core.Statistics;

namespace Popo.Api.Services.Statistics;

public sealed class BondDailyStatisticsService(
    IPortfolioCurrencyRatesProvider currencyRatesProvider,
    BondDailyChangesCalculator calculator,
    BondDailyTopTurnoversCalculator turnoversCalculator,
    CurrentBondDailyPriceComparisonCalculator currentCalculator,
    HistoricalBondDailyPriceComparisonCalculator historicalCalculator,
    TimeProvider timeProvider)
{
    public async Task<BondDailyStatisticsResult> GetDailyStatisticsAsync(
        DateOnly date,
        CancellationToken cancellationToken,
        int minimumTrades = 5,
        double minimumTurnoverRub = 1_000_000,
        BondIssuerCategory? issuerCategory = null)
    {
        var today = DateOnly.FromDateTime(MoscowTime.ToMoscow(timeProvider.GetUtcNow()).DateTime);

        var priceCalculator = GetCalculator(date, today);
        var comparison = await priceCalculator.CalculateAsync(date, cancellationToken);

        if (comparison is null)
            return new BondDailyStatisticsResult(date, null, [], [], []);

        return await CreateResultAsync(
            date,
            comparison,
            minimumTrades,
            minimumTurnoverRub,
            issuerCategory,
            cancellationToken);
    }

    private async Task<BondDailyStatisticsResult> CreateResultAsync(
        DateOnly date,
        BondDailyPriceComparison comparison,
        int minimumTrades,
        double minimumTurnoverRub,
        BondIssuerCategory? issuerCategory,
        CancellationToken cancellationToken)
    {
        var previousPrices = FilterByIssuerCategory(comparison.PreviousPrices, issuerCategory);
        var currentPrices = await FilterLiquidPricesAsync(
            comparison.CurrentPrices, date, minimumTrades, minimumTurnoverRub, issuerCategory, cancellationToken);

        var turnovers = turnoversCalculator.Calculate(currentPrices.Turnovers);
        var previousKeys = previousPrices
            .Where(IsValidPrice)
            .Select(price => (price.SecId, price.BoardId))
            .ToHashSet();
        var hasComparablePrices = currentPrices.Prices.Any(price =>
            IsValidPrice(price) && previousKeys.Contains((price.SecId, price.BoardId)));

        if (!hasComparablePrices)
            return new BondDailyStatisticsResult(date, null, [], [], turnovers);

        var changes = calculator.Calculate(previousPrices, currentPrices.Prices);
        return new BondDailyStatisticsResult(date, comparison.ComparedWithDate, changes.Declines, changes.Increases, turnovers);
    }

    private static bool IsValidPrice(BondDailyPricePoint price) =>
        !string.IsNullOrWhiteSpace(price.SecId)
        && !string.IsNullOrWhiteSpace(price.BoardId)
        && double.IsFinite(price.Price)
        && price.Price > 0;

    private async Task<(IReadOnlyCollection<BondDailyPricePoint> Prices, IReadOnlyList<BondDailyTurnover> Turnovers)> FilterLiquidPricesAsync(
        IReadOnlyCollection<BondDailyPricePoint> prices,
        DateOnly sessionDate,
        int minimumTrades,
        double minimumTurnoverRub,
        BondIssuerCategory? issuerCategory,
        CancellationToken cancellationToken)
    {
        var selectedPrices = FilterByIssuerCategory(prices, issuerCategory);
        var currencyIds = selectedPrices
            .Select(GetCurrencyId)
            .Where(currencyId => currencyId is not null && !IsRuble(currencyId))
            .Select(currencyId => currencyId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        IReadOnlyList<PortfolioCurrencyRateRecord> rates = currencyIds.Length == 0
            ? []
            : await currencyRatesProvider.GetLatestRatesAsync(currencyIds, sessionDate, cancellationToken);
        var rateByCurrencyId = rates
            .Where(rate => double.IsFinite(rate.RateToRub) && rate.RateToRub > 0)
            .ToDictionary(rate => rate.CurrencyId, rate => rate.RateToRub, StringComparer.OrdinalIgnoreCase);

        var filteredPrices = new List<BondDailyPricePoint>();
        var turnovers = new List<BondDailyTurnover>();
        foreach (var price in selectedPrices)
        {
            var turnoverRub = GetTurnoverRub(price, rateByCurrencyId);
            if (!MeetsLiquidityFilters(price, minimumTrades, minimumTurnoverRub, turnoverRub))
                continue;

            filteredPrices.Add(price);
            if (turnoverRub is > 0)
                turnovers.Add(new BondDailyTurnover(price.SecId, price.BoardId, price.ShortName, turnoverRub.Value, price.EmitterId));
        }

        return (filteredPrices, turnovers);
    }

    private static IReadOnlyCollection<BondDailyPricePoint> FilterByIssuerCategory(
        IReadOnlyCollection<BondDailyPricePoint> prices,
        BondIssuerCategory? issuerCategory) => issuerCategory is { } category
            ? prices.Where(price => price.IssuerCategory == category).ToArray()
            : prices;

    private static bool MeetsLiquidityFilters(
        BondDailyPricePoint price,
        int minimumTrades,
        double minimumTurnoverRub,
        double? turnoverRub)
    {
        if (minimumTrades > 0 && (price.NumTrades is not { } numTrades
            || !double.IsFinite(numTrades)
            || numTrades < minimumTrades))
        {
            return false;
        }

        if (minimumTurnoverRub == 0)
            return true;

        return turnoverRub is { } value && value >= minimumTurnoverRub;
    }

    private static double? GetTurnoverRub(
        BondDailyPricePoint price,
        IReadOnlyDictionary<string, double> rateByCurrencyId)
    {
        if (price.Value is not { } value || !double.IsFinite(value) || value < 0)
            return null;

        var currencyId = GetCurrencyId(price);
        if (currencyId is null)
            return null;

        var rateToRub = IsRuble(currencyId) ? 1 : rateByCurrencyId.GetValueOrDefault(currencyId);
        var turnoverRub = value * rateToRub;
        return rateToRub > 0 && double.IsFinite(turnoverRub) ? turnoverRub : null;
    }

    private static string? GetCurrencyId(BondDailyPricePoint price)
    {
        var currencyId = string.IsNullOrWhiteSpace(price.CurrencyId)
            ? RelationHelper.BoardCurrency.GetValueOrDefault(price.BoardId)
            : price.CurrencyId;
        return string.IsNullOrWhiteSpace(currencyId) ? null : currencyId.Trim().ToUpperInvariant();
    }

    private static bool IsRuble(string currencyId) => currencyId is "RUB" or "SUR" or "RUR";

    public IBondDailyPriceComparisonCalculator GetCalculator(DateOnly date, DateOnly today) =>
        (date == today) switch
        {
            true => currentCalculator,
            false => historicalCalculator
        };
}
