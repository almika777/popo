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
    private IsolatedTestDatabase _database = null!;
    private BondsService _service = null!;
    private string _allBoardSecId = null!;
    private string _nameSecId = null!;
    private string _shortNameSecId = null!;
    private string _unknownAccruedSecId = null!;

    [SetUp]
    public async Task SetUp()
    {
        _database = await TestDatabase.CreateIsolatedAsync();
        _service = new BondsService(new BondsProvider(_database.ContextFactory));
        _allBoardSecId = NewSecId();
        _nameSecId = NewSecId();
        _shortNameSecId = NewSecId();
        _unknownAccruedSecId = NewSecId();

        await using var context = await _database.ContextFactory.CreateDbContextAsync();
        context.MoexBondSecurities.AddRange(
            CreateBond(_allBoardSecId, "TQOB", "Alpha bond", "Alpha Corporation 001", "RUB", 11.25),
            CreateBond(_allBoardSecId, "TQCB", "Alpha bond currency", "Alpha Corporation 001 currency", "USD", 12.5),
            CreateBond(_nameSecId, "TQOB", "Beta bond", "Beta Issuer 002", "CNY", 7.75),
            CreateBond(_shortNameSecId, "TQOB", "Gamma lookup", "Different Issuer", "RUB", 3.5),
            CreateBond(_unknownAccruedSecId, "TQOB", "Unknown accrual", null, "RUB", null));
        await context.SaveChangesAsync();
    }

    [TearDown]
    public ValueTask TearDown() => _database.DisposeAsync();

    [Test]
    public async Task SearchAsync_ReturnsAllBoardVariantsInStableOrder()
    {
        var result = await _service.SearchAsync(_allBoardSecId, CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(x => x.BoardId), Is.EqualTo(new[] { "TQCB", "TQOB" }));
        Assert.That(result.Select(x => x.Currency), Is.EqualTo(new[] { "USD", "RUB" }));
    }

    [Test]
    public async Task SearchAsync_SearchesBySecNameAndReturnsTradingCurrency()
    {
        var result = await _service.SearchAsync("beta issuer", CancellationToken.None);

        var bond = result.Single();
        Assert.That(bond.SecId, Is.EqualTo(_nameSecId));
        Assert.That(bond.ShortName, Is.EqualTo("Beta bond"));
        Assert.That(bond.Currency, Is.EqualTo("CNY"));
        Assert.That(bond.AccruedInterest, Is.EqualTo(7.75));
    }

    [Test]
    public async Task SearchAsync_SearchesByShortName()
    {
        var result = await _service.SearchAsync("gamma lookup", CancellationToken.None);

        Assert.That(result.Select(x => x.SecId), Is.EqualTo(new[] { _shortNameSecId }));
    }

    [Test]
    public async Task SearchAsync_ReturnsEmptyListWhenNothingMatches()
    {
        var result = await _service.SearchAsync($"missing-{Guid.NewGuid():N}", CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task SearchAsync_PreservesNullAccruedInterestAndHandlesNullSecName()
    {
        var result = await _service.SearchAsync(_unknownAccruedSecId, CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].SecId, Is.EqualTo(_unknownAccruedSecId));
        Assert.That(result[0].AccruedInterest, Is.Null);
    }

    private static MoexBondSecurityEntity CreateBond(
        string secId,
        string boardId,
        string shortName,
        string? secName,
        string currencyId,
        double? accruedInterest) => new()
    {
        SecId = secId,
        BoardId = boardId,
        Isin = $"ISIN{Guid.NewGuid():N}"[..16],
        ShortName = shortName,
        SecName = secName,
        CurrencyId = currencyId,
        AccruedInt = accruedInterest,
        FaceValue = 1_000
    };

    private static string NewSecId() => $"B{Guid.NewGuid():N}"[..12];
}
