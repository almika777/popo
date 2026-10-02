using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;
using Popo.Core.Portfolio;
using Popo.Storage.Entities;
using Popo.Storage.Entities.Moex;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class PortfolioTradeFaceValueMigrationTests
{
    [Test]
    public async Task Migration_ReplacesStaleTradeFaceValuesWithNominalOnTradeDate()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await using var db = await database.ContextFactory.CreateDbContextAsync();
        var migrator = db.GetService<IMigrator>();
        var baselineMigration = db.Database.GetMigrations().Single(
            x => x.EndsWith("_SetPortfolioTradeFaceValueDefaultTo1000", StringComparison.Ordinal));
        await migrator.MigrateAsync(baselineMigration);

        var secId = "RU000A10F504";
        var boardId = "TQCB";
        var trades = new (DateOnly Date, double Quantity, double Price, double ExpectedFaceValue)[]
        {
            (new DateOnly(2026, 7, 22), 489, 1026.24, 1026.65),
            (new DateOnly(2026, 7, 28), 196, 1028.9, 1029.11),
            (new DateOnly(2026, 8, 7), 212, 1033.79, 1033.13),
            (new DateOnly(2026, 8, 11), 173, 1034.686, 1034.7),
            (new DateOnly(2026, 8, 12), 629, 1035.0858, 1035.1),
            (new DateOnly(2026, 8, 31), 125, 1041.1969, 1042.57)
        };
        foreach (var trade in trades)
        {
            db.MoexHistoryYieldsEntities.Add(new MoexHistoryYieldsEntity
            {
                SecId = secId,
                BoardId = boardId,
                TradeDate = trade.Date,
                FaceValue = trade.ExpectedFaceValue
            });
            db.PortfolioTrades.Add(new PortfolioTradeEntity
            {
                Id = Guid.NewGuid(),
                SecId = secId,
                BoardId = boardId,
                CurrencyId = "RUB",
                TradeDate = trade.Date,
                Side = TradeSide.Buy,
                Quantity = trade.Quantity,
                Price = trade.Price,
                FaceValue = 1000,
                AccruedInterest = 0,
                Commission = 0
            });
        }

        var noHistoryTradeDate = new DateOnly(2026, 9, 1);
        db.PortfolioTrades.Add(new PortfolioTradeEntity
        {
            Id = Guid.NewGuid(),
            SecId = secId,
            BoardId = boardId,
            CurrencyId = "RUB",
            TradeDate = noHistoryTradeDate,
            Side = TradeSide.Buy,
            Quantity = 1,
            Price = 1000,
            FaceValue = 1000,
            AccruedInterest = 0,
            Commission = 0
        });
        await db.SaveChangesAsync();

        await migrator.MigrateAsync();

        var savedTrades = await db.PortfolioTrades.AsNoTracking()
            .OrderBy(x => x.TradeDate)
            .ToArrayAsync();

        Assert.Multiple(() =>
        {
            Assert.That(savedTrades.Take(trades.Length).Select(x => x.FaceValue),
                Is.EqualTo(trades.Select(x => x.ExpectedFaceValue)));
            Assert.That(savedTrades[^1].FaceValue, Is.EqualTo(1000));
        });
    }
}
