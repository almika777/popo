using Popo.Api.Models;
using Popo.Api.Services.Position;
using Popo.Core.Bonds;
using Popo.Core.Common;
using Popo.Core.Portfolio;

namespace Popo.Api.Services;

/// <summary>Собирает текущие данные и выполняет stateless-срез портфеля по валютам.</summary>
public sealed class PortfolioCurrencyAnalyticsService(
    IPortfolioPositionsService positionsService,
    IPortfolioCurrencyRatesProvider currencyRatesProvider,
    IBondsService bondsService,
    PortfolioCurrencyAnalyticsCalculator calculator)
{
    /// <summary>Рассчитывает оценённую часть портфеля и доступные валюты ввода.</summary>
    /// <param name="request">Временные денежные остатки, цены и курсы пользователя.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Текущий валютный срез без сохранения переданных параметров.</returns>
    public async Task<PortfolioCurrencyAnalyticsResponse> CalculateAsync(
        PortfolioCurrencyAnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.CashBalances);
        ArgumentNullException.ThrowIfNull(request.ManualPrices);
        ArgumentNullException.ThrowIfNull(request.ManualRates);

        var asOf = MoscowTime.Today;
        var cashCurrencyIds = request.CashBalances
            .Select(balance => balance.CurrencyId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var positionsTask = positionsService.GetCurrencyAnalyticsAssetsAsync(cancellationToken);
        var fundsTask = positionsService.GetMoneyMarketFundsAsync(cancellationToken);
        var tradingCurrenciesTask = bondsService.GetTradingCurrenciesAsync(cancellationToken);
        var faceUnitsTask = bondsService.GetFaceUnitsAsync(cancellationToken);
        var availableRateCurrenciesTask = currencyRatesProvider.GetAvailableCurrencyIdsAsync(asOf, cancellationToken);
        var cashRatesTask = currencyRatesProvider.GetLatestRatesAsync(cashCurrencyIds, asOf, cancellationToken);
        await Task.WhenAll(
            positionsTask,
            fundsTask,
            tradingCurrenciesTask,
            faceUnitsTask,
            availableRateCurrenciesTask,
            cashRatesTask);

        var positions = await positionsTask;
        var funds = await fundsTask;
        var currentCashRates = await cashRatesTask;
        var assets = positions
            .Concat(funds.Select(ToAnalyticsAsset))
            .ToArray();
        var cashBalances = request.CashBalances
            .Select(balance => new PortfolioCurrencyAnalyticsCashBalance(
                balance.CurrencyId,
                balance.Amount,
                GetCurrentRate(balance.CurrencyId, currentCashRates)))
            .ToArray();
        var manualPrices = request.ManualPrices.ToDictionary(
            price => price.AssetId.Trim(),
            price => price.Price,
            StringComparer.OrdinalIgnoreCase);
        var manualRates = request.ManualRates.ToDictionary(
            rate => NormalizeCurrencyId(rate.CurrencyId),
            rate => rate.RateToRub,
            StringComparer.OrdinalIgnoreCase);
        var calculation = calculator.Calculate(new PortfolioCurrencyAnalyticsInput(
            assets,
            cashBalances,
            manualPrices,
            manualRates));

        var availableCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "RUB" };
        AddCurrencies(availableCurrencies, await tradingCurrenciesTask);
        AddCurrencies(availableCurrencies, await faceUnitsTask);
        AddCurrencies(availableCurrencies, await availableRateCurrenciesTask);
        AddCurrencies(availableCurrencies, cashCurrencyIds);
        foreach (var asset in positions)
        {
            AddCurrency(availableCurrencies, asset.TradingCurrencyId);
            AddCurrency(availableCurrencies, asset.FaceUnit);
        }

        return new PortfolioCurrencyAnalyticsResponse(
            asOf,
            calculation.TotalValueRub,
            availableCurrencies.OrderBy(currencyId => currencyId, StringComparer.Ordinal).ToArray(),
            calculation.Slices.Select(slice => new PortfolioCurrencyAnalyticsSliceResponse(
                slice.CurrencyId,
                slice.ValueRub,
                slice.Share)).ToArray(),
            calculation.UnvaluedAssets.Select(asset => new PortfolioCurrencyAnalyticsUnvaluedAssetResponse(
                asset.AssetId,
                asset.Kind,
                asset.Name,
                asset.CurrencyId,
                asset.RateCurrencyId,
                asset.MissingPrice,
                asset.MissingRate)).ToArray());
    }

    private static PortfolioCurrencyAnalyticsAsset ToAnalyticsAsset(MoneyMarketFundRecord fund) => new(
        $"fund:{fund.SecId}:{fund.BoardId}",
        PortfolioCurrencyAnalyticsAssetKind.MoneyMarketFund,
        fund.SecId,
        "RUB",
        null,
        fund.Quantity,
        fund.CurrentPrice,
        1);

    private static double? GetCurrentRate(
        string currencyId,
        IReadOnlyList<PortfolioCurrencyRateRecord> rates)
    {
        if (IsRubleCurrency(currencyId))
        {
            return 1;
        }

        return rates
            .FirstOrDefault(rate => string.Equals(
                rate.CurrencyId,
                currencyId,
                StringComparison.OrdinalIgnoreCase))
            ?.RateToRub;
    }

    private static void AddCurrencies(HashSet<string> target, IEnumerable<string> currencyIds)
    {
        foreach (var currencyId in currencyIds)
        {
            AddCurrency(target, currencyId);
        }
    }

    private static void AddCurrency(HashSet<string> target, string? currencyId)
    {
        if (!string.IsNullOrWhiteSpace(currencyId))
        {
            target.Add(NormalizeCurrencyId(currencyId));
        }
    }

    private static string NormalizeCurrencyId(string currencyId) => currencyId.Trim().ToUpperInvariant() switch
    {
        "SUR" or "RUR" => "RUB",
        var normalized => normalized
    };

    private static bool IsRubleCurrency(string currencyId) => NormalizeCurrencyId(currencyId) == "RUB";
}
