namespace Popo.Core.Portfolio.ClosedSessions;

public static class ClosedSessionCurrencyConverter
{
    public static bool IsRuble(string currency) => Normalize(currency) == "RUB";

    public static decimal? Convert(decimal? value, decimal? valueInRub, string faceUnit,
        string tradingCurrency, DateOnly date, IReadOnlyList<ClosedSessionCurrencyRate> rates)
    {
        if (Normalize(faceUnit) == Normalize(tradingCurrency) && value is >= 0)
            return value;
        if (IsRuble(tradingCurrency) && valueInRub is >= 0)
            return valueInRub;
        var tradingRate = GetRate(tradingCurrency, date, rates);
        if (tradingRate is not > 0)
            return null;
        if (valueInRub is >= 0)
            return valueInRub / tradingRate;
        var faceRate = GetRate(faceUnit, date, rates);
        return value is >= 0 && faceRate is > 0 ? value * faceRate / tradingRate : null;
    }

    private static decimal? GetRate(string currency, DateOnly date, IReadOnlyList<ClosedSessionCurrencyRate> rates)
        => IsRuble(currency) ? 1 : rates
            .Where(x => Normalize(x.Currency) == Normalize(currency) && x.Date <= date && x.UnitRate > 0)
            .OrderByDescending(x => x.Date).Select(x => (decimal?)x.UnitRate).FirstOrDefault();

    private static string Normalize(string value) => value.Trim().ToUpperInvariant() switch
    {
        "SUR" or "RUR" => "RUB",
        var currency => currency
    };
}
