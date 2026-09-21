using NUnit.Framework;
using Popo.Core.Portfolio;
using Popo.Storage.Providers.Portfolio;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class PortfolioLedgerProviderTests
{
    private IsolatedTestDatabase _database = null!;
    private PortfolioLedgerProvider _provider = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _database = await TestDatabase.CreateIsolatedAsync();
        _provider = new PortfolioLedgerProvider(_database.ContextFactory);
    }

    [OneTimeTearDown]
    public ValueTask OneTimeTearDown() => _database.DisposeAsync();

    [Test]
    public async Task Trade_CanBeCreatedUpdatedDeletedAndAggregated()
    {
        var secId = $"TEST-{Guid.NewGuid():N}";
        const string boardId = "TQCB";
        const string currencyId = "RUB";
        var buy = await _provider.AddTradeAsync(
            new PortfolioTrade(secId, boardId, currencyId, new DateOnly(2026, 1, 1), TradeSide.Buy, 100, 98),
            CancellationToken.None);
        await _provider.AddTradeAsync(
            new PortfolioTrade(secId, boardId, currencyId, new DateOnly(2026, 1, 2), TradeSide.Sell, 25, 99),
            CancellationToken.None);

        Assert.That(
            (await _provider.GetPositionsAsync(CancellationToken.None))
            .Single(x => x.SecId == secId && x.BoardId == boardId && x.CurrencyId == currencyId)
            .Quantity,
            Is.EqualTo(75));

        Assert.That(await _provider.UpdateTradeAsync(
            buy.Id,
            new PortfolioTrade(secId, boardId, currencyId, new DateOnly(2026, 1, 1), TradeSide.Buy, 80, 98),
            CancellationToken.None), Is.True);
        Assert.That(
            (await _provider.GetPositionsAsync(CancellationToken.None))
            .Single(x => x.SecId == secId && x.BoardId == boardId && x.CurrencyId == currencyId)
            .Quantity,
            Is.EqualTo(55));
        Assert.That(await _provider.DeleteTradeAsync(buy.Id, CancellationToken.None), Is.True);
    }

    [Test]
    public async Task CashSnapshot_IsCurrencySpecificAndCanBeUpdated()
    {
        var created = await _provider.AddCashSnapshotAsync(
            new CashSnapshot("RUB", new DateOnly(2026, 1, 1), 100_000),
            "start",
            CancellationToken.None);
        await _provider.AddTradeAsync(
            new PortfolioTrade("RU000B", "TQCB", "RUB", new DateOnly(2026, 1, 2), TradeSide.Buy, 10, 100),
            CancellationToken.None);

        var balance = (await _provider.GetCashBalancesAsync(new DateOnly(2026, 1, 2), CancellationToken.None)).Single();
        Assert.That(balance.CurrencyId, Is.EqualTo("RUB"));
        Assert.That(balance.Amount, Is.EqualTo(99_000));

        var updated = await _provider.UpdateCashSnapshotAsync(
            created.Id,
            new CashSnapshot("RUB", new DateOnly(2026, 1, 1), 90_000),
            "updated",
            CancellationToken.None);
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Amount, Is.EqualTo(90_000));
        Assert.That((await _provider.GetCashSnapshotAsync(created.Id, CancellationToken.None))!.Amount, Is.EqualTo(90_000));
        Assert.That(await _provider.DeleteCashSnapshotAsync(created.Id, CancellationToken.None), Is.True);
    }
}
