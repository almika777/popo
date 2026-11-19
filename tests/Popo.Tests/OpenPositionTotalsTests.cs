using NUnit.Framework;
using Popo.Api.Models;
using Popo.Api.Services.Position;
using Popo.Core.Portfolio;

namespace Popo.Tests;

public sealed class OpenPositionTotalsTests
{
    [Test]
    public void Calculate_ConvertsEachCurrencyBeforeSumming()
    {
        var positions = new[] { Position("RUB", 1_100, 100), Position("USD", 20, -5) };
        var rates = new[] { new PortfolioCurrencyRateRecord("USD", new DateOnly(2026, 1, 1), 100) };
        var result = OpenPositionTotalsCalculator.Calculate(positions, rates);
        Assert.Multiple(() =>
        {
            Assert.That(result.MarketValueRub, Is.EqualTo(3_100));
            Assert.That(result.InvestedAmountRub, Is.EqualTo(3_500));
            Assert.That(result.UnrealizedPnlRub, Is.EqualTo(-400));
            Assert.That(result.UnrealizedPnlPercent, Is.EqualTo(-400d / 3500 * 100).Within(0.000001));
        });
    }

    [Test]
    public void Calculate_WithoutExchangeRate_ReportsUnavailableTotals()
    {
        var result = OpenPositionTotalsCalculator.Calculate([Position("USD", 20, 5)], []);
        Assert.Multiple(() =>
        {
            Assert.That(result.MarketValueRub, Is.Null);
            Assert.That(result.InvestedAmountRub, Is.Null);
            Assert.That(result.UnrealizedPnlRub, Is.Null);
            Assert.That(result.UnrealizedPnlPercent, Is.Null);
        });
    }

    [TestCase("SUR")]
    [TestCase("RUR")]
    public void Calculate_RubleAliasesDoNotRequireExchangeRates(string currency)
    {
        var result = OpenPositionTotalsCalculator.Calculate([Position(currency, 1_100, 100)], []);
        Assert.That(result.MarketValueRub, Is.EqualTo(1_100));
    }

    private static PositionResponse Position(string currency, double value, double pnl) => new()
    {
        SecId = "BOND", BoardId = "TQCB", CurrencyId = currency, ShortName = "Bond",
        TotalCost = value, UnrealizedPnl = pnl
    };
}
