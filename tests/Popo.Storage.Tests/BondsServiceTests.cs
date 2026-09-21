using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Popo.Core.Bonds;
using Popo.Storage;
using Popo.Storage.Entities.Moex;
using Popo.Storage.Providers.Bonds;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class BondsServiceTests
{
    private BondsService _service = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await using var context = await TestDatabase.ContextFactory.CreateDbContextAsync();
        context.MoexBondSecurities.AddRange(
            new MoexBondSecurityEntity
            {
                SecId = "RU000A001BND",
                BoardId = "TQOB",
                Isin = "RU000A001BND",
                ShortName = "Alpha bond",
                SecName = "Alpha Corporation 001",
                FaceUnit = "RUB",
                CurrencyId = "RUB"
            },
            new MoexBondSecurityEntity
            {
                SecId = "RU000A001BND",
                BoardId = "TQCB",
                Isin = "RU000A001BND",
                ShortName = "Alpha bond currency",
                SecName = "Alpha Corporation 001 currency",
                FaceUnit = "RUB",
                CurrencyId = "USD"
            },
            new MoexBondSecurityEntity
            {
                SecId = "RU000A002BND",
                BoardId = "TQOB",
                Isin = "RU000A002BND",
                ShortName = "Beta bond",
                SecName = "Beta Issuer 002",
                FaceUnit = string.Empty,
                CurrencyId = "CNY"
            });
        await context.SaveChangesAsync();

        _service = new BondsService(new BondsProvider(TestDatabase.ContextFactory));
    }

    [Test]
    public async Task SearchAsync_ReturnsAllBoardVariantsForSecId()
    {
        var result = await _service.SearchAsync("001bnd", CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(x => x.BoardId), Is.EqualTo(new[] { "TQCB", "TQOB" }));
        Assert.That(result.Select(x => x.Currency), Is.EqualTo(new[] { "USD", "RUB" }));
    }

    [Test]
    public async Task SearchAsync_SearchesByNameAndUsesCurrencyFallback()
    {
        var result = await _service.SearchAsync("beta issuer", CancellationToken.None);

        var bond = result.Single();
        Assert.That(bond.SecId, Is.EqualTo("RU000A002BND"));
        Assert.That(bond.ShortName, Is.EqualTo("Beta bond"));
        Assert.That(bond.Currency, Is.EqualTo("CNY"));
    }
}
