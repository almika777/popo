namespace Popo.Api.Services.Position;

internal static class PositionCurrencyRates
{
    internal static double? ConvertCurrencyAmount(
        double amount,
        string sourceCurrency,
        string targetCurrency,
        IReadOnlyCollection<CurrencyRateSnapshot> rates,
        DateOnly date)
    {
        var sourceRate = GetCurrencyRate(rates, sourceCurrency, date);
        var targetRate = GetCurrencyRate(rates, targetCurrency, date);
        return sourceRate.HasValue && targetRate.HasValue ? amount * sourceRate.Value / targetRate.Value : null;
    }

    internal static double? GetCurrencyRate(
        IReadOnlyCollection<CurrencyRateSnapshot> rates,
        string currency,
        DateOnly date)
    {
        if (IsRubleCurrency(currency))
            return 1;

        return rates
            .Where(x => string.Equals(x.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase) &&
                        x.RateDate <= date)
            .OrderByDescending(x => x.RateDate)
            .Select(x => (double?)x.UnitRate)
            .FirstOrDefault();
    }

    internal static bool IsRubleCurrency(string currency) =>
        string.Equals(currency, "RUB", StringComparison.OrdinalIgnoreCase)
        || string.Equals(currency, "SUR", StringComparison.OrdinalIgnoreCase);

}
