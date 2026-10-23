namespace Popo.Core.Portfolio;

/// <summary>Рассчитывает текущую оценённую часть портфеля по валютам.</summary>
public sealed class PortfolioCurrencyAnalyticsCalculator
{
    /// <summary>Строит валютные доли из позиций, фондов и денежных остатков.</summary>
    /// <param name="input">Исходные данные и временные ручные значения.</param>
    /// <returns>Оценённая сумма, валютные доли и список неоценённых активов.</returns>
    public PortfolioCurrencyAnalyticsResult Calculate(PortfolioCurrencyAnalyticsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Assets);
        ArgumentNullException.ThrowIfNull(input.CashBalances);
        ArgumentNullException.ThrowIfNull(input.ManualPricesByAssetId);
        ArgumentNullException.ThrowIfNull(input.ManualRatesByCurrencyId);

        var currencyValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var unvaluedAssets = new List<PortfolioCurrencyAnalyticsUnvaluedAsset>();

        foreach (var asset in input.Assets)
        {
            ArgumentNullException.ThrowIfNull(asset);
            if (!double.IsFinite(asset.Quantity) || asset.Quantity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(input), "Количество актива должно быть конечным и неотрицательным.");
            }

            if (asset.Quantity == 0)
            {
                continue;
            }

            var tradingCurrencyId = NormalizeCurrencyId(asset.TradingCurrencyId);
            var groupingCurrencyId = NormalizeCurrencyId(
                string.IsNullOrWhiteSpace(asset.FaceUnit) ? tradingCurrencyId : asset.FaceUnit);
            var price = IsPositiveFinite(asset.MarketPrice)
                ? asset.MarketPrice
                : TryGetPositiveFinite(input.ManualPricesByAssetId, asset.AssetId);
            var rate = IsPositiveFinite(asset.TradingCurrencyRateToRub)
                ? asset.TradingCurrencyRateToRub
                : TryGetPositiveFinite(input.ManualRatesByCurrencyId, tradingCurrencyId);

            if (price is null || rate is null)
            {
                unvaluedAssets.Add(new PortfolioCurrencyAnalyticsUnvaluedAsset(
                    asset.AssetId,
                    asset.Kind,
                    asset.Name,
                    groupingCurrencyId,
                    price is null,
                    rate is null));
                continue;
            }

            AddValue(currencyValues, groupingCurrencyId, asset.Quantity * price.Value * rate.Value);
        }

        foreach (var cashBalance in input.CashBalances)
        {
            ArgumentNullException.ThrowIfNull(cashBalance);
            if (!double.IsFinite(cashBalance.Amount) || cashBalance.Amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(input), "Денежный остаток должен быть конечным и неотрицательным.");
            }

            if (cashBalance.Amount == 0)
            {
                continue;
            }

            var currencyId = NormalizeCurrencyId(cashBalance.CurrencyId);
            var rate = IsPositiveFinite(cashBalance.RateToRub)
                ? cashBalance.RateToRub
                : TryGetPositiveFinite(input.ManualRatesByCurrencyId, currencyId);

            if (rate is null)
            {
                unvaluedAssets.Add(new PortfolioCurrencyAnalyticsUnvaluedAsset(
                    $"cash:{currencyId}",
                    PortfolioCurrencyAnalyticsAssetKind.Cash,
                    $"Денежные средства {currencyId}",
                    currencyId,
                    MissingPrice: false,
                    MissingRate: true));
                continue;
            }

            AddValue(currencyValues, currencyId, cashBalance.Amount * rate.Value);
        }

        var totalValueRub = currencyValues.Values.Sum();
        if (!double.IsFinite(totalValueRub))
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Сумма портфеля должна быть конечной.");
        }

        var slices = totalValueRub <= 0
            ? []
            : currencyValues
                .Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new PortfolioCurrencyAnalyticsSlice(
                    pair.Key,
                    pair.Value,
                    pair.Value / totalValueRub))
                .ToArray();

        return new PortfolioCurrencyAnalyticsResult(totalValueRub, slices, unvaluedAssets);
    }

    private static void AddValue(IDictionary<string, double> currencyValues, string currencyId, double valueRub)
    {
        if (!double.IsFinite(valueRub) || valueRub < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valueRub), "Оценка актива должна быть конечной и неотрицательной.");
        }

        if (valueRub == 0)
        {
            return;
        }

        currencyValues.TryGetValue(currencyId, out var currentValue);
        currencyValues[currencyId] = currentValue + valueRub;
    }

    private static string NormalizeCurrencyId(string? currencyId)
    {
        if (string.IsNullOrWhiteSpace(currencyId))
        {
            throw new ArgumentException("Код валюты не может быть пустым.", nameof(currencyId));
        }

        return currencyId.Trim().ToUpperInvariant();
    }

    private static double? TryGetPositiveFinite(
        IReadOnlyDictionary<string, double> values,
        string key)
    {
        if (values.TryGetValue(key, out var directValue) && IsPositiveFinite(directValue))
        {
            return directValue;
        }

        foreach (var (candidate, value) in values)
        {
            if (string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase) && IsPositiveFinite(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool IsPositiveFinite(double? value) =>
        value is { } number && double.IsFinite(number) && number > 0;
}
