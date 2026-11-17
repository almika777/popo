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
    private PortfolioHoldingsProvider _positionsProvider = null!;
    private PortfolioTradesProvider _tradesProvider = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _database = await TestDatabase.CreateIsolatedAsync();
        _positionsProvider = new PortfolioHoldingsProvider(_database.ContextFactory);
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
            (await _positionsProvider.GetHoldingsAsync(new DateOnly(2026, 1, 2), CancellationToken.None))
            .Single(x => x.SecId == secId && x.BoardId == boardId && x.CurrencyId == currencyId)
            .Quantity,
            Is.EqualTo(75));

        Assert.That(await _tradesProvider.UpdateTradeAsync(
            buy.Id,
            new PortfolioTrade(secId, boardId, currencyId, new DateOnly(2026, 1, 1), TradeSide.Buy, 80, 98),
            CancellationToken.None), Is.True);
        Assert.That(
            (await _positionsProvider.GetHoldingsAsync(new DateOnly(2026, 1, 2), CancellationToken.None))
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

        var position = (await _positionsProvider.GetHoldingsAsync(asOf, CancellationToken.None))
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

        Assert.That((await _tradesProvider.GetTradesAsync(CancellationToken.None))
            .Single(x => x.Id == buy.Id).TradeDate, Is.EqualTo(new DateOnly(2026, 1, 1)));
    }

    [Test]
    public async Task UpdateTrade_RejectsSaleBeforeSameDayPurchaseInIdOrder()
    {
        var secId = $"TEST-{Guid.NewGuid():N}";
        var ids = new[] { Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7() }.Order().ToArray();
        await using (var context = await _database.ContextFactory.CreateDbContextAsync())
        {
            context.PortfolioTrades.AddRange(
                new PortfolioTradeEntity { Id = ids[0], SecId = secId, BoardId = "TQCB", CurrencyId = "RUB",
                    TradeDate = new DateOnly(2026, 1, 1), Side = TradeSide.Buy, Quantity = 4, Price = 100, FaceValue = 1_000 },
                new PortfolioTradeEntity { Id = ids[1], SecId = secId, BoardId = "TQCB", CurrencyId = "RUB",
                    TradeDate = new DateOnly(2026, 1, 3), Side = TradeSide.Sell, Quantity = 5, Price = 100, FaceValue = 1_000 },
                new PortfolioTradeEntity { Id = ids[2], SecId = secId, BoardId = "TQCB", CurrencyId = "RUB",
                    TradeDate = new DateOnly(2026, 1, 2), Side = TradeSide.Buy, Quantity = 2, Price = 100, FaceValue = 1_000 });
            await context.SaveChangesAsync();
        }

        Assert.ThrowsAsync<InvalidOperationException>(() => _tradesProvider.UpdateTradeAsync(ids[1],
            new PortfolioTrade(secId, "TQCB", "RUB", new DateOnly(2026, 1, 2), TradeSide.Sell, 5, 100),
            CancellationToken.None));

        var unchanged = (await _tradesProvider.GetTradesAsync(CancellationToken.None)).Single(trade => trade.Id == ids[1]);
        Assert.That(unchanged.TradeDate, Is.EqualTo(new DateOnly(2026, 1, 3)));
    }
}
