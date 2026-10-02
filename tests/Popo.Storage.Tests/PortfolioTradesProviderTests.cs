using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Popo.Core.Portfolio;
using Popo.Storage.Entities;
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
        var sell = await _tradesProvider.AddTradeAsync(
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
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _tradesProvider.DeleteTradeAsync(buy.Id, CancellationToken.None));
        Assert.That(await _tradesProvider.DeleteTradeAsync(sell.Id, CancellationToken.None), Is.True);
        Assert.That(await _tradesProvider.DeleteTradeAsync(buy.Id, CancellationToken.None), Is.True);
    }

    [Test]
    public async Task AddTrade_UnrelatedPositionIsAllowedWhenAnotherPositionHasAnInvalidSequence()
    {
        var invalidSecId = $"TEST-{Guid.NewGuid():N}";
        const string boardId = "TQCB";
        const string currencyId = "RUB";
        var invalidSellId = Guid.NewGuid();
        await using (var context = await _database.ContextFactory.CreateDbContextAsync())
        {
            context.PortfolioTrades.Add(new PortfolioTradeEntity
            {
                Id = invalidSellId,
                SecId = invalidSecId,
                BoardId = boardId,
                CurrencyId = currencyId,
                TradeDate = new DateOnly(2026, 1, 2),
                Side = TradeSide.Sell,
                Quantity = 1,
                Price = 99,
                FaceValue = 1_000
            });
            await context.SaveChangesAsync();
        }

        var unrelatedTrade = await _tradesProvider.AddTradeAsync(
            new PortfolioTrade($"TEST-{Guid.NewGuid():N}", boardId, currencyId,
                new DateOnly(2026, 1, 1), TradeSide.Buy, 1, 98),
            CancellationToken.None);

        Assert.That(await _tradesProvider.DeleteTradeAsync(unrelatedTrade.Id, CancellationToken.None), Is.True);
        await using var cleanupContext = await _database.ContextFactory.CreateDbContextAsync();
        await cleanupContext.PortfolioTrades.Where(x => x.Id == invalidSellId).ExecuteDeleteAsync();
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

    [Test]
    public async Task AddTrade_RejectsSaleBeforeLaterPurchaseEvenWhenFinalQuantityIsPositive()
    {
        var secId = $"TEST-{Guid.NewGuid():N}";
        const string boardId = "TQCB";
        const string currencyId = "RUB";
        await _tradesProvider.AddTradeAsync(
            new PortfolioTrade(secId, boardId, currencyId, new DateOnly(2026, 1, 2), TradeSide.Buy, 10, 98),
            CancellationToken.None);

        Assert.ThrowsAsync<InvalidOperationException>(() => _tradesProvider.AddTradeAsync(
            new PortfolioTrade(secId, boardId, currencyId, new DateOnly(2026, 1, 1), TradeSide.Sell, 5, 99),
            CancellationToken.None));

        Assert.That((await _tradesProvider.GetTradesAsync(CancellationToken.None)).Count(x => x.SecId == secId), Is.EqualTo(1));
    }

    [Test]
    public async Task UpdateTrade_RejectsChangingBuyToAfterExistingSale()
    {
        var secId = $"TEST-{Guid.NewGuid():N}";
        const string boardId = "TQCB";
        const string currencyId = "RUB";
        var buy = await _tradesProvider.AddTradeAsync(
            new PortfolioTrade(secId, boardId, currencyId, new DateOnly(2026, 1, 1), TradeSide.Buy, 10, 98),
            CancellationToken.None);
        await _tradesProvider.AddTradeAsync(
            new PortfolioTrade(secId, boardId, currencyId, new DateOnly(2026, 1, 2), TradeSide.Sell, 5, 99),
            CancellationToken.None);

        Assert.ThrowsAsync<InvalidOperationException>(() => _tradesProvider.UpdateTradeAsync(
            buy.Id,
            new PortfolioTrade(secId, boardId, currencyId, new DateOnly(2026, 1, 3), TradeSide.Buy, 10, 98),
            CancellationToken.None));

        Assert.That((await _tradesProvider.GetTradeAsync(buy.Id, CancellationToken.None))?.TradeDate, Is.EqualTo(new DateOnly(2026, 1, 1)));
    }
}
