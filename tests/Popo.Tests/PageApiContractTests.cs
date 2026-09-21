using NUnit.Framework;
using Popo.Api.Models;
using Popo.Core.Portfolio;
using Popo.Core.PortfolioReturns;

namespace Popo.Tests;

public sealed class PageApiContractTests
{
    [Test]
    public void CashPageResponse_ContainsAllInitialTabData()
    {
        var response = new CashPageResponse([], [], ["RUB"], [], [], []);

        Assert.Multiple(() =>
        {
            Assert.That(response.Currencies, Is.EqualTo(new[] { "RUB" }));
            Assert.That(response.Snapshots, Is.Empty);
            Assert.That(response.Balances, Is.Empty);
            Assert.That(response.MoneyMarketFunds, Is.Empty);
            Assert.That(response.MoneyMarketFundOptions, Is.Empty);
            Assert.That(response.MoneyMarketFundOperations, Is.Empty);
        });
    }

    [Test]
    public void PortfolioPageResponse_ContainsAllInitialTabData()
    {
        var summary = new PortfolioSummaryRecord(new DateOnly(2026, 9, 18), 100, 80, 20, 0, 5);
        var response = new PortfolioPageResponse([], [], [], summary);

        Assert.That(response.Summary, Is.SameAs(summary));
    }

    [Test]
    public void CashRecommendationsPageResponse_ContainsPresetsAndCurrencyFilters()
    {
        var response = new CashRecommendationsPageResponse([], ["RUB"], ["RUB", "USD"]);

        Assert.Multiple(() =>
        {
            Assert.That(response.Presets, Is.Empty);
            Assert.That(response.TradingCurrencies, Is.EqualTo(new[] { "RUB" }));
            Assert.That(response.FaceUnits, Is.EqualTo(new[] { "RUB", "USD" }));
        });
    }
}
