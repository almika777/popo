using Popo.Core.Common;
using Popo.Core.Portfolio;

namespace Popo.Api.Services.Statistics;

internal static class BondDailyTurnoverRubConverter
{
    public static async Task<IReadOnlyDictionary<string, double>> GetRatesToRubAsync(
        IPortfolioCurrencyRatesProvider currencyRatesProvider,
        IEnumerable<string?> currencyIds,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var requestedCurrencies = currencyIds
            .Select(NormalizeCurrencyId)
            .Where(currencyId => currencyId is not null && currencyId != "RUB")
            .Select(currencyId => currencyId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var ratesToRub = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["RUB"] = 1 };
        if (requestedCurrencies.Length == 0)
            return ratesToRub;

        var rates = await currencyRatesProvider.GetLatestRatesAsync(requestedCurrencies, asOf, cancellationToken);
        foreach (var rate in rates.Where(rate => double.IsFinite(rate.RateToRub) && rate.RateToRub > 0))
        {
            if (NormalizeCurrencyId(rate.CurrencyId) is { } currencyId)
                ratesToRub[currencyId] = rate.RateToRub;
        }

        return ratesToRub;
    }

    public static string? GetCurrencyId(string? currencyId, string? boardId)
    {
        var normalizedCurrencyId = NormalizeCurrencyId(currencyId);
        if (!string.IsNullOrEmpty(normalizedCurrencyId))
            return normalizedCurrencyId;

        return boardId is null
            ? null
            : NormalizeCurrencyId(RelationHelper.BoardCurrency.GetValueOrDefault(boardId));
    }

    public static double? ConvertToRub(
        double? turnover,
        string? currencyId,
        IReadOnlyDictionary<string, double> ratesToRub)
    {
        if (turnover is not { } amount || !double.IsFinite(amount) || amount <= 0 ||
            GetCurrencyId(currencyId, null) is not { } normalizedCurrencyId ||
            !ratesToRub.TryGetValue(normalizedCurrencyId, out var rateToRub))
            return null;

        var turnoverRub = amount * rateToRub;
        return double.IsFinite(turnoverRub) && turnoverRub > 0 ? turnoverRub : null;
    }

    private static string? NormalizeCurrencyId(string? currencyId) => currencyId?.Trim().ToUpperInvariant() switch
    {
        null or "" => null,
        "RUR" or "SUR" => "RUB",
        var value => value
    };
}
