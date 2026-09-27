using NUnit.Framework;
using Popo.Core.PortfolioReturns;
using Popo.Storage.Providers.PortfolioReturns;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class PortfolioCashFlowsProviderTests
{
    private IsolatedTestDatabase _database = null!;
    private PortfolioCashFlowsProvider _provider = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _database = await TestDatabase.CreateIsolatedAsync();
        _provider = new PortfolioCashFlowsProvider(_database.ContextFactory);
    }

    [OneTimeTearDown]
    public ValueTask OneTimeTearDown() => _database.DisposeAsync();

    [Test]
    public async Task CashFlow_PreservesTypeAndAmount()
    {
        var created = await _provider.AddCashFlowAsync(
            new DateOnly(2026, 1, 10),
            PortfolioCashFlowType.Deposit,
            25_000,
            "deposit",
            CancellationToken.None);

        var stored = await _provider.GetCashFlowAsync(created.Id, CancellationToken.None);

        Assert.That(stored, Is.Not.Null);
        Assert.That(stored!.Type, Is.EqualTo(PortfolioCashFlowType.Deposit));
        Assert.That(stored.Amount, Is.EqualTo(25_000));
        Assert.That(await _provider.DeleteCashFlowAsync(created.Id, CancellationToken.None), Is.True);
    }
}
