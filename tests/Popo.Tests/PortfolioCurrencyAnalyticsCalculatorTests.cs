using NUnit.Framework;
using Popo.Core.Portfolio;

namespace Popo.Tests;

public sealed class PortfolioCurrencyAnalyticsCalculatorTests
{
    private readonly PortfolioCurrencyAnalyticsCalculator _calculator = new();

    [Test]
    public void PositionCurrencyGroupUsesFaceUnitButValuationUsesTradingCurrency()
    {
        var result = Calculate(
            [Asset("bond", "RUB", faceUnit: "CNY", quantity: 2, marketPrice: 500, rateToRub: 1)]);

        Assert.That(result.TotalValueRub, Is.EqualTo(1_000));
        Assert.That(result.Slices, Has.Count.EqualTo(1));
        Assert.That(result.Slices[0].CurrencyId, Is.EqualTo("CNY"));
        Assert.That(result.Slices[0].ValueRub, Is.EqualTo(1_000));
        Assert.That(result.Slices[0].Share, Is.EqualTo(1));
    }

    [Test]
    public void PositionFallsBackToTradingCurrencyWhenFaceUnitMissing()
    {
        var result = Calculate(
            [Asset("bond", "USD", quantity: 2, marketPrice: 10, rateToRub: 90)]);

        Assert.That(result.Slices.Single().CurrencyId, Is.EqualTo("USD"));
        Assert.That(result.Slices.Single().ValueRub, Is.EqualTo(1_800));
    }

    [Test]
    public void CalculateIncludesPositionsCashAndFundsInTotalAndShares()
    {
        var result = _calculator.Calculate(new PortfolioCurrencyAnalyticsInput(
            [
                Asset("bond", "USD", quantity: 2, marketPrice: 10, rateToRub: 90),
                Asset("fund", "RUB", quantity: 5, marketPrice: 100, rateToRub: 1,
                    kind: PortfolioCurrencyAnalyticsAssetKind.MoneyMarketFund)
            ],
            [new PortfolioCurrencyAnalyticsCashBalance("RUB", 10_000, 1)],
            EmptyValues(),
            EmptyValues()));

        Assert.That(result.TotalValueRub, Is.EqualTo(12_300));
        Assert.That(result.Slices, Has.Count.EqualTo(2));
        Assert.That(result.Slices.Single(x => x.CurrencyId == "USD").ValueRub, Is.EqualTo(1_800));
        Assert.That(result.Slices.Single(x => x.CurrencyId == "USD").Share, Is.EqualTo(1_800d / 12_300).Within(0.000001));
        Assert.That(result.Slices.Single(x => x.CurrencyId == "RUB").ValueRub, Is.EqualTo(10_500));
        Assert.That(result.Slices.Sum(x => x.Share), Is.EqualTo(1).Within(0.000001));
    }

    [Test]
    public void CurrentMarketPriceTakesPrecedenceOverManualPrice()
    {
        var result = _calculator.Calculate(new PortfolioCurrencyAnalyticsInput(
            [Asset("bond", "RUB", quantity: 2, marketPrice: 100, rateToRub: 1)],
            [],
            Values(("bond", 150)),
            EmptyValues()));

        Assert.That(result.TotalValueRub, Is.EqualTo(200));
        Assert.That(result.UnvaluedAssets, Is.Empty);
    }

    [Test]
    public void ManualPriceUsesQuantityAndTradingCurrency()
    {
        var result = _calculator.Calculate(new PortfolioCurrencyAnalyticsInput(
            [Asset("bond", "RUB", faceUnit: "CNY", quantity: 3, marketPrice: null, rateToRub: 1)],
            [],
            Values(("bond", 50)),
            EmptyValues()));

        Assert.That(result.TotalValueRub, Is.EqualTo(150));
        Assert.That(result.Slices.Single().CurrencyId, Is.EqualTo("CNY"));
    }

    [Test]
    public void ManualRateIsUsedOnlyWhenCurrentRateIsMissing()
    {
        var currentRateResult = _calculator.Calculate(new PortfolioCurrencyAnalyticsInput(
            [Asset("bond", "USD", quantity: 2, marketPrice: 50, rateToRub: 90)],
            [],
            EmptyValues(),
            Values(("USD", 120))));

        var manualRateResult = _calculator.Calculate(new PortfolioCurrencyAnalyticsInput(
            [Asset("bond", "USD", quantity: 2, marketPrice: 50, rateToRub: null)],
            [],
            EmptyValues(),
            Values(("USD", 120))));

        Assert.That(currentRateResult.TotalValueRub, Is.EqualTo(9_000));
        Assert.That(manualRateResult.TotalValueRub, Is.EqualTo(12_000));
    }

    [Test]
    public void UnvaluedAssetReportsEachMissingInput()
    {
        var result = Calculate(
            [Asset("bond", "CNY", faceUnit: "CNY", quantity: 2, marketPrice: null, rateToRub: null)]);

        Assert.That(result.TotalValueRub, Is.Zero);
        Assert.That(result.Slices, Is.Empty);
        Assert.That(result.UnvaluedAssets, Has.Count.EqualTo(1));
        Assert.That(result.UnvaluedAssets[0].AssetId, Is.EqualTo("bond"));
        Assert.That(result.UnvaluedAssets[0].MissingPrice, Is.True);
        Assert.That(result.UnvaluedAssets[0].MissingRate, Is.True);
    }

    [Test]
    public void UnvaluedAssetSeparatesFaceCurrencyGroupFromTradingCurrencyRate()
    {
        var result = Calculate(
            [Asset("bond", "USD", faceUnit: "CNY", quantity: 2, marketPrice: null, rateToRub: null)]);

        Assert.That(result.UnvaluedAssets.Single().CurrencyId, Is.EqualTo("CNY"));
        Assert.That(result.UnvaluedAssets.Single().RateCurrencyId, Is.EqualTo("USD"));
    }

    [Test]
    public void ManualPriceAndRateResolveUnvaluedAsset()
    {
        var result = _calculator.Calculate(new PortfolioCurrencyAnalyticsInput(
            [Asset("bond", "USD", quantity: 2, marketPrice: null, rateToRub: null)],
            [],
            Values(("bond", 10)),
            Values(("USD", 90))));

        Assert.That(result.TotalValueRub, Is.EqualTo(1_800));
        Assert.That(result.UnvaluedAssets, Is.Empty);
    }

    [Test]
    public void UnquotedFundCanBeManuallyValued()
    {
        var result = _calculator.Calculate(new PortfolioCurrencyAnalyticsInput(
            [Asset("fund", "RUB", quantity: 2, marketPrice: null, rateToRub: 1,
                kind: PortfolioCurrencyAnalyticsAssetKind.MoneyMarketFund)],
            [],
            Values(("fund", 500)),
            EmptyValues()));

        Assert.That(result.TotalValueRub, Is.EqualTo(1_000));
        Assert.That(result.UnvaluedAssets, Is.Empty);
    }

    [Test]
    public void EmptyPortfolioReturnsNoSlices()
    {
        var result = Calculate([]);

        Assert.That(result.TotalValueRub, Is.Zero);
        Assert.That(result.Slices, Is.Empty);
        Assert.That(result.UnvaluedAssets, Is.Empty);
    }

    [Test]
    public void ZeroCashBalanceDoesNotRequireCurrencyRate()
    {
        var result = _calculator.Calculate(new PortfolioCurrencyAnalyticsInput(
            [],
            [new PortfolioCurrencyAnalyticsCashBalance("CNY", 0, null)],
            EmptyValues(),
            EmptyValues()));

        Assert.That(result.TotalValueRub, Is.Zero);
        Assert.That(result.Slices, Is.Empty);
        Assert.That(result.UnvaluedAssets, Is.Empty);
    }

    [Test]
    public void CashWithoutCurrentRateIsShownAndCanUseManualRate()
    {
        var missingRate = _calculator.Calculate(new PortfolioCurrencyAnalyticsInput(
            [],
            [new PortfolioCurrencyAnalyticsCashBalance("CNY", 100, null)],
            EmptyValues(),
            EmptyValues()));
        var manuallyValued = _calculator.Calculate(new PortfolioCurrencyAnalyticsInput(
            [],
            [new PortfolioCurrencyAnalyticsCashBalance("CNY", 100, null)],
            EmptyValues(),
            Values(("CNY", 12))));

        Assert.That(missingRate.UnvaluedAssets, Has.Count.EqualTo(1));
        Assert.That(missingRate.UnvaluedAssets[0].Kind, Is.EqualTo(PortfolioCurrencyAnalyticsAssetKind.Cash));
        Assert.That(missingRate.UnvaluedAssets[0].MissingPrice, Is.False);
        Assert.That(missingRate.UnvaluedAssets[0].MissingRate, Is.True);
        Assert.That(manuallyValued.TotalValueRub, Is.EqualTo(1_200));
        Assert.That(manuallyValued.UnvaluedAssets, Is.Empty);
    }

    [Test]
    public void CurrencyGroupingIsCaseInsensitive()
    {
        var result = Calculate(
        [
            Asset("bond-1", "USD", quantity: 1, marketPrice: 10, rateToRub: 90),
            Asset("bond-2", "usd", quantity: 1, marketPrice: 10, rateToRub: 90)
        ]);

        Assert.That(result.Slices, Has.Count.EqualTo(1));
        Assert.That(result.Slices[0].ValueRub, Is.EqualTo(1_800));
        Assert.That(result.Slices[0].Share, Is.EqualTo(1));
    }

    [Test]
    public void RubleCurrencyAliasesAreGroupedWithRubleFundsAndCash()
    {
        var result = _calculator.Calculate(new PortfolioCurrencyAnalyticsInput(
            [
                Asset("bond", "RUB", faceUnit: "SUR", quantity: 1, marketPrice: 100, rateToRub: 1),
                Asset("legacy-bond", "RUB", faceUnit: "RUR", quantity: 1, marketPrice: 100, rateToRub: 1),
                Asset("fund", "RUB", faceUnit: "RUB", quantity: 1, marketPrice: 100, rateToRub: 1,
                    kind: PortfolioCurrencyAnalyticsAssetKind.MoneyMarketFund)
            ],
            [new PortfolioCurrencyAnalyticsCashBalance("RUB", 100, 1)],
            EmptyValues(),
            EmptyValues()));

        Assert.That(result.Slices, Has.Count.EqualTo(1));
        Assert.That(result.Slices.Single().CurrencyId, Is.EqualTo("RUB"));
        Assert.That(result.Slices.Single().ValueRub, Is.EqualTo(400));
    }

    private PortfolioCurrencyAnalyticsResult Calculate(
        IReadOnlyList<PortfolioCurrencyAnalyticsAsset> assets) => _calculator.Calculate(
        new PortfolioCurrencyAnalyticsInput(assets, [], EmptyValues(), EmptyValues()));

    private static PortfolioCurrencyAnalyticsAsset Asset(
        string assetId,
        string tradingCurrencyId,
        string? faceUnit = null,
        double quantity = 1,
        double? marketPrice = 100,
        double? rateToRub = 1,
        PortfolioCurrencyAnalyticsAssetKind kind = PortfolioCurrencyAnalyticsAssetKind.Position) =>
        new(assetId, kind, assetId, tradingCurrencyId, faceUnit, quantity, marketPrice, rateToRub);

    private static IReadOnlyDictionary<string, double> EmptyValues() =>
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, double> Values((string Key, double Value) value) =>
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { [value.Key] = value.Value };
}
