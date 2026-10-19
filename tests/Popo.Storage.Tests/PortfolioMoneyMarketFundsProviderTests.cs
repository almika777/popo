using NUnit.Framework;
using Popo.Core.Portfolio;
using Popo.Storage.Providers.Portfolio;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class PortfolioMoneyMarketFundsProviderTests
{
    private IsolatedTestDatabase _database = null!;
    private PortfolioMoneyMarketFundsProvider _provider = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _database = await TestDatabase.CreateIsolatedAsync();
        _provider = new PortfolioMoneyMarketFundsProvider(_database.ContextFactory);
    }

    [OneTimeTearDown]
    public ValueTask OneTimeTearDown() => _database.DisposeAsync();

    [Test]
    public async Task GetMoneyMarketFunds_ReturnsTheSavedCurrentPosition()
    {
        var secId = $"F{Guid.NewGuid():N}";
        await _provider.AddMoneyMarketFundAsync(
            new MoneyMarketFund(secId, "TQBR", 110, 10.875),
            CancellationToken.None);

        var currentFund = (await _provider.GetMoneyMarketFundsAsync(CancellationToken.None)).Single(x =>
            string.Equals(x.SecId, secId, StringComparison.OrdinalIgnoreCase));

        Assert.That(currentFund.Quantity, Is.EqualTo(110));
        Assert.That(currentFund.AveragePrice, Is.EqualTo(10.875).Within(0.000001));
    }

    [Test]
    public async Task UpdateAndDeleteMoneyMarketFund_ChangeOnlyTheSavedCurrentPosition()
    {
        var secId = $"F{Guid.NewGuid():N}";
        var added = await _provider.AddMoneyMarketFundAsync(
            new MoneyMarketFund(secId, "TQBR", 5, 10),
            CancellationToken.None);

        var updated = await _provider.UpdateMoneyMarketFundAsync(
            added.Id,
            new MoneyMarketFund(secId, "TQBR", 12, 11.25),
            CancellationToken.None);
        var current = (await _provider.GetMoneyMarketFundsAsync(CancellationToken.None)).Single(x => x.Id == added.Id);

        Assert.Multiple(() =>
        {
            Assert.That(updated!.Quantity, Is.EqualTo(12));
            Assert.That(updated.AveragePrice, Is.EqualTo(11.25));
            Assert.That(current.Quantity, Is.EqualTo(12));
            Assert.That(current.AveragePrice, Is.EqualTo(11.25));
        });
        Assert.That(await _provider.DeleteMoneyMarketFundAsync(added.Id, CancellationToken.None), Is.True);
        Assert.That((await _provider.GetMoneyMarketFundsAsync(CancellationToken.None)).Any(x => x.Id == added.Id), Is.False);
    }
}
