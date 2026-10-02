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
    public async Task GetMoneyMarketFunds_ReplaysPurchasesAndSalesAgainstBasePosition()
    {
        var secId = $"F{Guid.NewGuid():N}";
        await _provider.AddMoneyMarketFundAsync(
            new MoneyMarketFund(secId, "TQBR", 100, 10),
            CancellationToken.None);
        await _provider.AddMoneyMarketFundOperationAsync(
            new MoneyMarketFundOperation(secId, new DateOnly(2026, 1, 2), TradeSide.Buy, 20, 15, 5),
            CancellationToken.None);
        await _provider.AddMoneyMarketFundOperationAsync(
            new MoneyMarketFundOperation(secId, new DateOnly(2026, 1, 3), TradeSide.Sell, 10, 12, 1),
            CancellationToken.None);

        var currentFund = (await _provider.GetMoneyMarketFundsAsync(CancellationToken.None)).Single(x =>
            string.Equals(x.SecId, secId, StringComparison.OrdinalIgnoreCase));

        Assert.That(currentFund.Quantity, Is.EqualTo(110));
        Assert.That(currentFund.AveragePrice, Is.EqualTo(10.875).Within(0.000001));
    }

    [Test]
    public async Task AddMoneyMarketFundOperation_RejectsSaleAboveAvailableQuantity()
    {
        var secId = $"F{Guid.NewGuid():N}";
        await _provider.AddMoneyMarketFundAsync(
            new MoneyMarketFund(secId, "TQBR", 5, 10),
            CancellationToken.None);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _provider.AddMoneyMarketFundOperationAsync(
                new MoneyMarketFundOperation(secId, new DateOnly(2026, 1, 2), TradeSide.Sell, 6, 10, 0),
                CancellationToken.None));
    }
}
