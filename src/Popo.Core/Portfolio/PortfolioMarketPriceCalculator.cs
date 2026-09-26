namespace Popo.Core.Portfolio;

public static class PortfolioMarketPriceCalculator
{
    public static double? CalculatePricePercent(
        double price,
        double faceValue,
        string faceUnit,
        string tradingCurrency,
        double? faceCurrencyRateToRub,
        double? tradingCurrencyRateToRub)
    {
        if (!double.IsFinite(price) || price <= 0
            || !double.IsFinite(faceValue) || faceValue <= 0
            || string.IsNullOrWhiteSpace(faceUnit)
            || string.IsNullOrWhiteSpace(tradingCurrency))
        {
            return null;
        }

        var faceRate = IsRubleCurrency(faceUnit) ? 1 : faceCurrencyRateToRub;
        var tradingRate = IsRubleCurrency(tradingCurrency) ? 1 : tradingCurrencyRateToRub;
        if (!IsPositiveFinite(faceRate) || !IsPositiveFinite(tradingRate))
        {
            return null;
        }

        var pricePercent = price * tradingRate!.Value / faceRate!.Value / faceValue * 100;
        return double.IsFinite(pricePercent) && pricePercent > 0 ? pricePercent : null;
    }

    public static double? Calculate(
        double faceValue,
        string faceUnit,
        string tradingCurrency,
        double? currentPricePercent,
        double? faceCurrencyRateToRub,
        double? tradingCurrencyRateToRub)
    {
        if (!double.IsFinite(faceValue) || faceValue <= 0
            || string.IsNullOrWhiteSpace(faceUnit)
            || string.IsNullOrWhiteSpace(tradingCurrency)
            || !currentPricePercent.HasValue
            || !double.IsFinite(currentPricePercent.Value)
            || currentPricePercent.Value <= 0)
        {
            return null;
        }

        var faceRate = IsRubleCurrency(faceUnit)
            ? 1
            : faceCurrencyRateToRub;
        var tradingRate = IsRubleCurrency(tradingCurrency)
            ? 1
            : tradingCurrencyRateToRub;

        if (!faceRate.HasValue || !tradingRate.HasValue
            || !double.IsFinite(faceRate.Value) || faceRate.Value <= 0
            || !double.IsFinite(tradingRate.Value) || tradingRate.Value <= 0)
        {
            return null;
        }

        return faceValue * currentPricePercent.Value / 100 * faceRate.Value / tradingRate.Value;
    }

    private static bool IsRubleCurrency(string currency) =>
        string.Equals(currency, "RUB", StringComparison.OrdinalIgnoreCase)
        || string.Equals(currency, "SUR", StringComparison.OrdinalIgnoreCase);

    private static bool IsPositiveFinite(double? value) =>
        value.HasValue && double.IsFinite(value.Value) && value.Value > 0;
}
