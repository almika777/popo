using NUnit.Framework;
using Popo.Core.PortfolioReturns;
using Popo.Storage.Providers.PortfolioReturns;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class PortfolioValuationsProviderTests
{
    private IsolatedTestDatabase _database = null!;
    private PortfolioValuationsProvider _provider = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _database = await TestDatabase.CreateIsolatedAsync();
        _provider = new PortfolioValuationsProvider(_database.ContextFactory);
    }

    [OneTimeTearDown]
    public ValueTask OneTimeTearDown() => _database.DisposeAsync();

    [Test]
    public async Task Valuation_CanBeCreatedUpdatedAndDeleted()
    {
        var created = await _provider.AddValuationAsync(
            new DateOnly(2026, 1, 1),
            100_000,
            "start",
            CancellationToken.None);

        Assert.That((await _provider.GetValuationsAsync(CancellationToken.None)).Single().TotalValue,
            Is.EqualTo(100_000));

        Assert.That(await _provider.UpdateValuationAsync(
            created.Id,
            new DateOnly(2026, 1, 2),
            101_000,
            "updated",
            CancellationToken.None), Is.True);
        Assert.That((await _provider.GetValuationAsync(created.Id, CancellationToken.None))!.TotalValue,
            Is.EqualTo(101_000));
        Assert.That(await _provider.DeleteValuationAsync(created.Id, CancellationToken.None), Is.True);
        Assert.That(await _provider.GetValuationAsync(created.Id, CancellationToken.None), Is.Null);
    }
}
