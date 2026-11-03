using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Popo.Core.Statistics;
using Popo.Storage.Entities.Moex;
using Popo.Storage.Providers.Bonds;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class BondDailyPriceHistoryProviderTests
{
    [Test]
    public async Task GetLatestTradingDateBeforeAsync_ReturnsPreviousAvailableDate()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var provider = new BondDailyPriceHistoryProvider(database.ContextFactory);
        var before = new DateOnly(2026, 10, 20);
        await AddPricesAsync(database,
            Price("BOND-A", "TQCB", before.AddDays(-2), 100),
            Price("BOND-B", "TQCB", before.AddDays(-1), 100),
            Price("BOND-C", "TQCB", before, 100));

        var result = await provider.GetLatestTradingDateBeforeAsync(before, CancellationToken.None);

        Assert.That(result, Is.EqualTo(before.AddDays(-1)));
    }

    [Test]
    public async Task GetClosingPricesAsync_ProjectsLastTradeCloseAndSkipsMissingClose()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var provider = new BondDailyPriceHistoryProvider(database.ContextFactory);
        var date = new DateOnly(2026, 10, 20);
        await AddPricesAsync(database,
            Price("BOND-A", "TQCB", date, 100, 90, "Короткое имя", 12, 1_200_000, "RUB"),
            Price("BOND-A", "CETS", date, 99, 89),
            Price("BOND-B", "TQCB", date, null, 95),
            Price("BOND-C", "TQCB", date.AddDays(-1), 98, 88));

        var result = await provider.GetClosingPricesAsync(date, CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result, Is.EquivalentTo(new[]
        {
            new BondDailyPricePoint("BOND-A", "TQCB", "Короткое имя", 100, 12, 1_200_000, "RUB"),
            new BondDailyPricePoint("BOND-A", "CETS", "BOND-A", 99)
        }));
    }

    [Test]
    public async Task GetClosingPricesAsync_IncludesIssuerCategoryFromMoexType()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var provider = new BondDailyPriceHistoryProvider(database.ContextFactory);
        var date = new DateOnly(2026, 10, 20);
        await using (var context = await database.ContextFactory.CreateDbContextAsync())
        {
            context.MoexBonds.AddRange(
                new MoexBondEntity { SecId = "OFZ", ShortName = "ОФЗ 26200", Type = "ofz_bond" },
                new MoexBondEntity { SecId = "REGION", ShortName = "Областная облигация", Type = "subfederal_bond" },
                new MoexBondEntity { SecId = "MUNICIPAL", ShortName = "Муниципальная облигация", Type = "municipal_bond" },
                new MoexBondEntity { SecId = "CORPORATE", ShortName = "Корпоративная облигация", Type = "corporate_bond", EmitterId = 11004 },
                new MoexBondEntity { SecId = "EXCHANGE", ShortName = "Биржевая облигация", Type = "exchange_bond" },
                new MoexBondEntity { SecId = "OTHER", ShortName = "Другая облигация", Type = "unknown_type" });
            context.MoexHistoryYieldsEntities.AddRange(
                Price("OFZ", "TQOB", date, 100),
                Price("REGION", "TQCB", date, 100),
                Price("MUNICIPAL", "TQCB", date, 100),
                Price("CORPORATE", "TQCB", date, 100),
                Price("EXCHANGE", "TQCB", date, 100),
                Price("OTHER", "TQCB", date, 100));
            await context.SaveChangesAsync();
        }

        var result = await provider.GetClosingPricesAsync(date, CancellationToken.None);
        var categories = result.ToDictionary(
            price => price.SecId,
            price => price.IssuerCategory?.ToString());

        Assert.That(categories, Is.EquivalentTo(new Dictionary<string, string?>
        {
            ["OFZ"] = "Government",
            ["REGION"] = "Municipal",
            ["MUNICIPAL"] = "Municipal",
            ["CORPORATE"] = "Corporate",
            ["EXCHANGE"] = "Corporate",
            ["OTHER"] = null
        }));
        Assert.That(result.Single(price => price.SecId == "CORPORATE").EmitterId, Is.EqualTo(11004));

        var liveMetadata = await provider.GetSecurityMetadataAsync(
            ["OFZ", "REGION", "MUNICIPAL", "CORPORATE", "EXCHANGE", "OTHER"],
            CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(liveMetadata["OFZ"], Is.EqualTo(new BondDailySecurityMetadata("ОФЗ 26200", BondIssuerCategory.Government)));
            Assert.That(liveMetadata["REGION"], Is.EqualTo(new BondDailySecurityMetadata("Областная облигация", BondIssuerCategory.Municipal)));
            Assert.That(liveMetadata["MUNICIPAL"], Is.EqualTo(new BondDailySecurityMetadata("Муниципальная облигация", BondIssuerCategory.Municipal)));
            Assert.That(liveMetadata["CORPORATE"], Is.EqualTo(new BondDailySecurityMetadata("Корпоративная облигация", BondIssuerCategory.Corporate, 11004)));
            Assert.That(liveMetadata["EXCHANGE"], Is.EqualTo(new BondDailySecurityMetadata("Биржевая облигация", BondIssuerCategory.Corporate)));
            Assert.That(liveMetadata["OTHER"], Is.EqualTo(new BondDailySecurityMetadata("Другая облигация", null)));
        });
    }

    private static async Task AddPricesAsync(IsolatedTestDatabase database, params MoexHistoryYieldsEntity[] prices)
    {
        await using var context = await database.ContextFactory.CreateDbContextAsync();
        context.MoexHistoryYieldsEntities.AddRange(prices);
        await context.SaveChangesAsync();
    }

    private static MoexHistoryYieldsEntity Price(
        string secId,
        string boardId,
        DateOnly date,
        double? close,
        double? legalClosePrice = null,
        string? shortName = null,
        double? numTrades = null,
        double? value = null,
        string? currencyId = null) => new()
    {
        SecId = secId,
        BoardId = boardId,
        TradeDate = date,
        Close = close,
        NumTrades = numTrades,
        Value = value,
        CurrencyId = currencyId,
        LegalClosePrice = legalClosePrice,
        ShortName = shortName
    };
}
