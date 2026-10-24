using NUnit.Framework;
using System.Text.Json;
using Popo.Api.Models;
using Popo.Api.Services;
using Popo.Api.Services.Position;
using Popo.Core.Bonds;
using Popo.Core.Common;
using Popo.Core.Portfolio;
using Popo.Core.PortfolioReturns;

namespace Popo.Tests;

public sealed class PortfolioCurrencyAnalyticsServiceTests
{
    [Test]
    public async Task CalculateAsyncCombinesPositionsCashAndLiquidity()
    {
        var positions = new FakePositionsService(
            [Asset("position:bond:TQCB:RUB", "RUB", "CNY", 2, 500, 1)],
            [Fund("LQDT", 5, 100)]);
        var service = CreateService(positions, [Rate("RUB", 1), Rate("USD", 90)]);

        var result = await service.CalculateAsync(Request(cash: [new("RUB", 10_000)]), CancellationToken.None);

        Assert.That(result.TotalValueRub, Is.EqualTo(11_500));
        Assert.That(result.Slices, Has.Count.EqualTo(2));
        Assert.That(result.Slices.Single(x => x.CurrencyId == "CNY").ValueRub, Is.EqualTo(1_000));
        Assert.That(result.Slices.Single(x => x.CurrencyId == "RUB").ValueRub, Is.EqualTo(10_500));
        Assert.That(result.Slices.Sum(x => x.Share), Is.EqualTo(1).Within(0.000001));
        Assert.That(result.UnvaluedAssets, Is.Empty);

        var serialized = JsonSerializer.SerializeToElement(result);
        var items = serialized.GetProperty("Items").EnumerateArray().ToArray();
        Assert.That(items, Has.Length.EqualTo(3));
        Assert.That(items.Select(item => item.GetProperty("Kind").GetInt32()), Is.EquivalentTo(new[] { 0, 1, 2 }));
        Assert.That(items.Single(item => item.GetProperty("AssetId").GetString() == "position:bond:TQCB:RUB")
            .GetProperty("CurrencyId").GetString(), Is.EqualTo("CNY"));
        Assert.That(items.Single(item => item.GetProperty("AssetId").GetString() == "position:bond:TQCB:RUB")
            .GetProperty("ValueRub").GetDouble(), Is.EqualTo(1_000));
        Assert.That(items.Single(item => item.GetProperty("Kind").GetInt32() == 2)
            .GetProperty("ValueRub").GetDouble(), Is.EqualTo(10_000));
    }

    [Test]
    public async Task CalculateAsyncUsesManualRateWhenLatestRateIsMissing()
    {
        var service = CreateService(new FakePositionsService(), []);

        var result = await service.CalculateAsync(
            Request(
                cash: [new("USD", 10)],
                manualRates: [new("USD", 90)]),
            CancellationToken.None);

        Assert.That(result.TotalValueRub, Is.EqualTo(900));
        Assert.That(result.Slices.Single().CurrencyId, Is.EqualTo("USD"));
        Assert.That(result.UnvaluedAssets, Is.Empty);
    }

    [Test]
    public async Task CalculateAsyncTrimsCashCurrencyBeforeChoosingCurrentOrManualRate()
    {
        var service = CreateService(new FakePositionsService(), [Rate("USD", 90)]);

        var result = await service.CalculateAsync(
            Request(
                cash: [new(" USD ", 1)],
                manualRates: [new("USD", 100)]),
            CancellationToken.None);

        Assert.That(result.TotalValueRub, Is.EqualTo(90));
        Assert.That(result.Slices.Single().CurrencyId, Is.EqualTo("USD"));
        Assert.That(result.UnvaluedAssets, Is.Empty);
    }

    [Test]
    public async Task CalculateAsyncUsesManualRateToValuePositionWithKnownPrice()
    {
        var positions = new FakePositionsService(
            [Asset("position:bond:TQCB:USD", "USD", "USD", 1, 950, null)]);
        var service = CreateService(positions, []);

        var result = await service.CalculateAsync(
            Request(manualRates: [new("USD", 90)]),
            CancellationToken.None);

        Assert.That(result.TotalValueRub, Is.EqualTo(85_500));
        Assert.That(result.Slices.Single().CurrencyId, Is.EqualTo("USD"));
        Assert.That(result.UnvaluedAssets, Is.Empty);
    }

    [Test]
    public async Task CalculateAsyncReturnsPartialItemsForMissingMarketData()
    {
        var positions = new FakePositionsService(
            [Asset("position:bond:TQCB:USD", "USD", "CNY", 2, null, null)],
            [Fund("TMON", 3, null)]);
        var service = CreateService(positions, []);

        var result = await service.CalculateAsync(Request(), CancellationToken.None);

        Assert.That(result.TotalValueRub, Is.Zero);
        Assert.That(result.UnvaluedAssets, Has.Count.EqualTo(2));
        Assert.That(result.UnvaluedAssets.Select(x => x.Kind), Is.EquivalentTo(new[]
        {
            PortfolioCurrencyAnalyticsAssetKind.Position,
            PortfolioCurrencyAnalyticsAssetKind.MoneyMarketFund
        }));
        Assert.That(result.UnvaluedAssets.Single(x => x.Kind == PortfolioCurrencyAnalyticsAssetKind.Position).MissingPrice, Is.True);
        Assert.That(result.UnvaluedAssets.Single(x => x.Kind == PortfolioCurrencyAnalyticsAssetKind.Position).MissingRate, Is.True);
        Assert.That(result.UnvaluedAssets.Single(x => x.Kind == PortfolioCurrencyAnalyticsAssetKind.MoneyMarketFund).MissingRate, Is.False);
    }

    [Test]
    public async Task CalculateAsyncDoesNotRetainManualInputsBetweenRequests()
    {
        var positions = new FakePositionsService(
            [Asset("position:bond:TQCB:USD", "USD", null, 2, null, null)]);
        var service = CreateService(positions, []);

        var manuallyValued = await service.CalculateAsync(
            Request(manualPrices: [new("position:bond:TQCB:USD", 10)], manualRates: [new("USD", 90)]),
            CancellationToken.None);
        var withoutManualValues = await service.CalculateAsync(Request(), CancellationToken.None);

        Assert.That(manuallyValued.TotalValueRub, Is.EqualTo(1_800));
        Assert.That(withoutManualValues.TotalValueRub, Is.Zero);
        Assert.That(withoutManualValues.UnvaluedAssets, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CalculateAsyncIncludesFaceAndTradingCurrenciesInCashOptions()
    {
        var positions = new FakePositionsService(
            [Asset("position:bond:TQCB:USD", "USD", "CNY", 1, 10, 90)]);
        var bonds = new FakeBondsService(["USD"], ["CNY"]);
        var service = CreateService(positions, [Rate("EUR", 100)], bonds);

        var result = await service.CalculateAsync(Request(), CancellationToken.None);

        Assert.That(result.AvailableCurrencies, Is.EqualTo(new[] { "CNY", "EUR", "RUB", "USD" }));
    }

    [Test]
    public void CalculateAsyncPropagatesPositionLoadFailureInsteadOfReturningZeroPortfolio()
    {
        var service = CreateService(new FakePositionsService(throwOnLoad: true), []);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.CalculateAsync(Request(), CancellationToken.None));
    }

    private static PortfolioCurrencyAnalyticsService CreateService(
        FakePositionsService positions,
        IReadOnlyList<PortfolioCurrencyRateRecord> rates,
        FakeBondsService? bonds = null) => new(
        positions,
        new FakeCurrencyRatesProvider(rates),
        bonds ?? new FakeBondsService(),
        new PortfolioCurrencyAnalyticsCalculator());

    private static PortfolioCurrencyAnalyticsRequest Request(
        IReadOnlyList<PortfolioCurrencyAnalyticsCashBalanceRequest>? cash = null,
        IReadOnlyList<PortfolioCurrencyAnalyticsManualPriceRequest>? manualPrices = null,
        IReadOnlyList<PortfolioCurrencyAnalyticsManualRateRequest>? manualRates = null) => new(
        cash ?? [],
        manualPrices ?? [],
        manualRates ?? []);

    private static PortfolioCurrencyAnalyticsAsset Asset(
        string assetId,
        string tradingCurrencyId,
        string? faceUnit,
        double quantity,
        double? marketPrice,
        double? rateToRub) => new(
        assetId,
        PortfolioCurrencyAnalyticsAssetKind.Position,
        assetId,
        tradingCurrencyId,
        faceUnit,
        quantity,
        marketPrice,
        rateToRub);

    private static MoneyMarketFundRecord Fund(string secId, double quantity, double? currentPrice) => new(
        Guid.NewGuid(), secId, "TQBR", quantity, 100, currentPrice, currentPrice.HasValue ? quantity * currentPrice.Value : null);

    private static PortfolioCurrencyRateRecord Rate(string currencyId, double rateToRub) => new(
        currencyId, MoscowTime.Today, rateToRub);

    private sealed class FakePositionsService(
        IReadOnlyList<PortfolioCurrencyAnalyticsAsset>? assets = null,
        IReadOnlyList<MoneyMarketFundRecord>? funds = null,
        bool throwOnLoad = false) : IPortfolioPositionsService
    {
        public Task<PositionsPageResponse> GetPageAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PortfolioOverview> GetOverviewAsync(DateOnly asOf, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(funds ?? Array.Empty<MoneyMarketFundRecord>());

        public Task<IReadOnlyList<PortfolioCurrencyAnalyticsAsset>> GetCurrencyAnalyticsAssetsAsync(CancellationToken cancellationToken) =>
            throwOnLoad
                ? Task.FromException<IReadOnlyList<PortfolioCurrencyAnalyticsAsset>>(new InvalidOperationException("Позиции недоступны."))
                : Task.FromResult(assets ?? Array.Empty<PortfolioCurrencyAnalyticsAsset>());
    }

    private sealed class FakeCurrencyRatesProvider(IReadOnlyList<PortfolioCurrencyRateRecord> rates)
        : IPortfolioCurrencyRatesProvider
    {
        public Task<IReadOnlyList<PortfolioCurrencyRateRecord>> GetLatestRatesAsync(
            IReadOnlyCollection<string> currencyIds,
            DateOnly asOf,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PortfolioCurrencyRateRecord>>(rates
                .Where(rate => currencyIds.Contains(rate.CurrencyId, StringComparer.OrdinalIgnoreCase)
                               && rate.RateDate <= asOf)
                .ToArray());

        public Task<IReadOnlyList<string>> GetAvailableCurrencyIdsAsync(
            DateOnly asOf,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(rates
                .Where(rate => rate.RateDate <= asOf)
                .Select(rate => rate.CurrencyId)
                .ToArray());
    }

    private sealed class FakeBondsService(
        IReadOnlyList<string>? tradingCurrencies = null,
        IReadOnlyList<string>? faceUnits = null) : IBondsService
    {
        public Task<IReadOnlyList<BondSearchResult>> SearchAsync(string query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BondFaceValueResult>> GetHistoricalFaceValuesAsync(
            IReadOnlyList<BondFaceValueRequest> requests,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetTradingCurrenciesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(tradingCurrencies ?? Array.Empty<string>());

        public Task<IReadOnlyList<string>> GetFaceUnitsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(faceUnits ?? Array.Empty<string>());
    }
}
