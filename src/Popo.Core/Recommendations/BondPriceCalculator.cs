namespace Popo.Core.Recommendations;

public static class BondPriceCalculator
{
    public static double? CalculateDirtyPrice(
        double cleanPricePercent,
        double faceValueInSettlementCurrency,
        double? accruedInterest)
    {
        if (!double.IsFinite(cleanPricePercent) || cleanPricePercent <= 0d
            || !double.IsFinite(faceValueInSettlementCurrency) || faceValueInSettlementCurrency <= 0d
            || !accruedInterest.HasValue
            || !double.IsFinite(accruedInterest.Value)
            || accruedInterest.Value < 0d)
        {
            return null;
        }

        var dirtyPrice = cleanPricePercent / 100d 
                         * faceValueInSettlementCurrency
                         + accruedInterest.Value;
        return double.IsFinite(dirtyPrice) ? dirtyPrice : null;
    }

    public static double? CalculateDirtyPrice(
        double cleanPricePercent,
        double faceValue,
        string faceUnit,
        string? currencyId,
        double? accruedInterest)
    {
        if (!double.IsFinite(cleanPricePercent) || cleanPricePercent <= 0d
            || !double.IsFinite(faceValue) || faceValue <= 0d
            || string.IsNullOrWhiteSpace(faceUnit)
            || string.IsNullOrWhiteSpace(currencyId)
            || !CurrenciesMatch(faceUnit, currencyId)
            || !accruedInterest.HasValue
            || !double.IsFinite(accruedInterest.Value)
            || accruedInterest.Value < 0d)
        {
            return null;
        }

        return CalculateDirtyPrice(cleanPricePercent, faceValue, accruedInterest);
    }

    private static bool CurrenciesMatch(string faceUnit, string currencyId) =>
        NormalizeCurrency(faceUnit) == NormalizeCurrency(currencyId);

    private static string NormalizeCurrency(string currency) =>
        currency.Trim().ToUpperInvariant() switch
        {
            "SUR" or "RUR" => "RUB",
            var normalized => normalized
        };
}
