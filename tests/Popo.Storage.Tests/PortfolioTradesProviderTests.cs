using NUnit.Framework;
using Popo.Core.Portfolio;
using Popo.Storage.Providers.Portfolio;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class PortfolioTradesProviderTests
{
    private IsolatedTestDatabase _database = null!;
    private PortfolioPositionsProvider _positionsProvider = null!;
    private PortfolioTradesProvider _tradesProvider = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _database = await TestDatabase.CreateIsolatedAsync();
        _positionsProvider = new PortfolioPositionsProvider(_database.ContextFactory);
        _tradesProvider = new PortfolioTradesProvider(_database.ContextFactory);
    }

    [OneTimeTearDown]
    public ValueTask OneTimeTearDown() => _database.DisposeAsync();

    [Test]
    public async Task TradeMutations_AreReflectedInLedgerPositions()
    {
        var secId = $"TEST-{Guid.NewGuid():N}";
        const string boardId = "TQCB";
        const string currencyId = "RUB";
        var buy = await _tradesProvider.AddTradeAsync(
            new PortfolioTrade(secId, boardId, currencyId, new DateOnly(2026, 1, 1), TradeSide.Buy, 100, 98),
            CancellationToken.None);
        await _tradesProvider.AddTradeAsync(
            new PortfolioTrade(secId, boardId, currencyId, new DateOnly(2026, 1, 2), TradeSide.Sell, 25, 99),
            CancellationToken.None);

        Assert.That(
            (await _positionsProvider.GetPositionsAsync(new DateOnly(2026, 1, 2), CancellationToken.None))
            .Single(x => x.SecId == secId && x.BoardId == boardId && x.CurrencyId == currencyId)
            .Quantity,
            Is.EqualTo(75));

        Assert.That(await _tradesProvider.UpdateTradeAsync(
            buy.Id,
            new PortfolioTrade(secId, boardId, currencyId, new DateOnly(2026, 1, 1), TradeSide.Buy, 80, 98),
            CancellationToken.None), Is.True);
        Assert.That(
            (await _positionsProvider.GetPositionsAsync(new DateOnly(2026, 1, 2), CancellationToken.None))
            .Single(x => x.SecId == secId && x.BoardId == boardId && x.CurrencyId == currencyId)
            .Quantity,
            Is.EqualTo(55));
        Assert.That(await _tradesProvider.DeleteTradeAsync(buy.Id, CancellationToken.None), Is.True);
    }

    [Test]
    public async Task FutureDatedTrades_AreExcludedFromPositionsAsOf()
    {
        var secId = $"TEST-{Guid.NewGuid():N}";
        const string boardId = "TQCB";
        const string currencyId = "RUB";
        var asOf = new DateOnly(2026, 1, 1);
        await _tradesProvider.AddTradeAsync(
            new PortfolioTrade(secId, boardId, currencyId, asOf, TradeSide.Buy, 100, 98),
            CancellationToken.None);
        await _tradesProvider.AddTradeAsync(
            new PortfolioTrade(secId, boardId, currencyId, asOf.AddDays(1), TradeSide.Buy, 10, 99),
            CancellationToken.None);

        var position = (await _positionsProvider.GetPositionsAsync(asOf, CancellationToken.None))
            .Single(x => x.SecId == secId && x.BoardId == boardId && x.CurrencyId == currencyId);

        Assert.That(position.Quantity, Is.EqualTo(100));
    }
}
