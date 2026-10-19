using NUnit.Framework;
using Microsoft.AspNetCore.Mvc.Routing;
using Popo.Api.Models;
using Popo.Api.Controllers;
using Popo.Core.Portfolio;
using Popo.Core.PortfolioReturns;
using System.Reflection;

namespace Popo.Tests;

public sealed class PageApiContractTests
{
    [Test]
    public void MoneyMarketFundsPageResponse_ContainsOnlyFundsTabData()
    {
        var response = new MoneyMarketFundsPageResponse([], [], []);

        Assert.Multiple(() =>
        {
            Assert.That(response.MoneyMarketFunds, Is.Empty);
            Assert.That(response.MoneyMarketFundOptions, Is.Empty);
            Assert.That(response.MoneyMarketFundOperations, Is.Empty);
        });
    }

    [Test]
    public void PortfolioPageResponse_ContainsAllInitialTabData()
    {
        var summary = new PortfolioSummaryRecord(new DateOnly(2026, 9, 18), 100, 80, 20);
        var response = new PortfolioPageResponse([], [], summary);

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

    [Test]
    public void LiquidityController_ExposesOnlyMoneyMarketFundEndpoints()
    {
        var endpoints = typeof(LiquidityController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>()
                .SelectMany(attribute => attribute.HttpMethods.Select(httpMethod =>
                    $"{httpMethod} {attribute.Template}")))
            .ToArray();

        Assert.That(endpoints, Is.EquivalentTo(new[]
        {
            "GET money-market-funds/page",
            "POST money-market-funds",
            "PUT money-market-funds/{id:guid}",
            "DELETE money-market-funds/{id:guid}",
            "POST money-market-fund-operations"
        }));
    }
}
