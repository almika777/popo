using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Popo.Core.Portfolio;
using Popo.Storage.Entities;
using Popo.Storage.Providers.Portfolio;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class PortfolioCurrencyRatesProviderTests
{
    [Test]
    public async Task GetLatestRatesAsyncReturnsMostRecentRateNotAfterAsOf()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var asOf = new DateOnly(2026, 10, 20);
        await AddRatesAsync(database,
            Rate(new DateOnly(2026, 10, 17), "USD", 90),
            Rate(new DateOnly(2026, 10, 19), "USD", 91),
            Rate(new DateOnly(2026, 10, 21), "USD", 99));
        var provider = new PortfolioCurrencyRatesProvider(database.ContextFactory);

        var result = await provider.GetLatestRatesAsync(["USD"], asOf, CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].CurrencyId, Is.EqualTo("USD"));
        Assert.That(result[0].RateDate, Is.EqualTo(new DateOnly(2026, 10, 19)));
        Assert.That(result[0].RateToRub, Is.EqualTo(91));
    }

    [Test]
    public async Task GetLatestRatesAsyncExcludesFutureRatesAndUnrequestedCurrencies()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var asOf = new DateOnly(2026, 10, 20);
        await AddRatesAsync(database,
            Rate(new DateOnly(2026, 10, 19), "USD", 91),
            Rate(new DateOnly(2026, 10, 19), "CNY", 13),
            Rate(new DateOnly(2026, 10, 21), "USD", 99));
        var provider = new PortfolioCurrencyRatesProvider(database.ContextFactory);

        var result = await provider.GetLatestRatesAsync(["USD"], asOf, CancellationToken.None);

        Assert.That(result.Select(x => x.CurrencyId), Is.EquivalentTo(new[] { "USD" }));
        Assert.That(result.Single().RateToRub, Is.EqualTo(91));
    }

    [Test]
    public async Task GetLatestRatesAsyncReturnsEmptyForUnknownCurrency()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        await AddRatesAsync(database, Rate(new DateOnly(2026, 10, 19), "USD", 91));
        var provider = new PortfolioCurrencyRatesProvider(database.ContextFactory);

        var result = await provider.GetLatestRatesAsync(["CNY"], new DateOnly(2026, 10, 20), CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetAvailableCurrencyIdsAsyncReturnsCurrenciesWithRatesNotAfterAsOf()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var asOf = new DateOnly(2026, 10, 20);
        await AddRatesAsync(database,
            Rate(new DateOnly(2026, 10, 19), "USD", 91),
            Rate(new DateOnly(2026, 10, 18), "CNY", 13),
            Rate(new DateOnly(2026, 10, 21), "EUR", 100));
        var provider = new PortfolioCurrencyRatesProvider(database.ContextFactory);

        var result = await provider.GetAvailableCurrencyIdsAsync(asOf, CancellationToken.None);

        Assert.That(result, Is.EqualTo(new[] { "CNY", "USD" }));
    }

    [Test]
    public async Task ReadMethodsDoNotChangeStoredRates()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var asOf = new DateOnly(2026, 10, 20);
        await AddRatesAsync(database, Rate(new DateOnly(2026, 10, 19), "USD", 91));
        var provider = new PortfolioCurrencyRatesProvider(database.ContextFactory);

        await provider.GetLatestRatesAsync(["USD"], asOf, CancellationToken.None);
        await provider.GetAvailableCurrencyIdsAsync(asOf, CancellationToken.None);

        await using var db = await database.ContextFactory.CreateDbContextAsync();
        Assert.That(await db.CurrencyRates.CountAsync(), Is.EqualTo(1));
    }

    private static async Task AddRatesAsync(IsolatedTestDatabase database, params CurrencyRateEntity[] rates)
    {
        await using var db = await database.ContextFactory.CreateDbContextAsync();
        db.CurrencyRates.AddRange(rates);
        await db.SaveChangesAsync();
    }

    private static CurrencyRateEntity Rate(DateOnly date, string currency, double rate) => new()
    {
        RateDate = date,
        CurrencyCode = currency,
        Name = currency,
        Nominal = 1,
        Value = rate,
        UnitRate = rate
    };
}
