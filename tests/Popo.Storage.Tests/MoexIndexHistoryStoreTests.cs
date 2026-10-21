using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;
using Popo.Core.Contracts.Iss;
using Popo.Storage.Providers;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class MoexIndexHistoryStoreTests
{
    [Test]
    public async Task Upsert_updates_existing_index_date_and_keeps_other_dates_and_indexes()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync(noTracking: true);
        var store = new MoexIndexHistoryStore(database.ContextFactory);
        var firstDate = new DateOnly(2022, 7, 22);
        var nextDate = new DateOnly(2022, 7, 25);

        await store.UpsertAsync(
        [
            Point("LQDTM", firstDate, 1.0000m),
            Point("LQDTM", nextDate, 1.0001m),
            Point("OTHER", firstDate, 100m)
        ], CancellationToken.None);
        await store.UpsertAsync(
        [
            Point("LQDTM", firstDate, 1.0002m)
        ], CancellationToken.None);

        var lqdt = await store.GetHistoryAsync("LQDTM", firstDate, nextDate, CancellationToken.None);
        var other = await store.GetHistoryAsync("OTHER", firstDate, firstDate, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(lqdt, Has.Count.EqualTo(2));
            Assert.That(lqdt.Select(x => x.TradeDate), Is.EqualTo(new DateOnly?[] { firstDate, nextDate }));
            Assert.That(lqdt[0].Close, Is.EqualTo(1.0002m));
            Assert.That(other, Has.Count.EqualTo(1));
            Assert.That(other[0].Close, Is.EqualTo(100m));
        });
    }

    [Test]
    public async Task GetHistory_returns_only_requested_index_and_inclusive_date_range_in_order()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var store = new MoexIndexHistoryStore(database.ContextFactory);
        var dates = new[]
        {
            new DateOnly(2022, 7, 22),
            new DateOnly(2022, 7, 25),
            new DateOnly(2022, 7, 26)
        };
        await store.UpsertAsync(
        [
            Point("LQDTM", dates[2], 1.0002m),
            Point("OTHER", dates[1], 100m),
            Point("LQDTM", dates[0], 1.0000m),
            Point("LQDTM", dates[1], 1.0001m)
        ], CancellationToken.None);

        var result = await store.GetHistoryAsync("LQDTM", dates[1], dates[2], CancellationToken.None);

        Assert.That(result.Select(x => x.TradeDate), Is.EqualTo(new DateOnly?[] { dates[1], dates[2] }));
        Assert.That(result.Select(x => x.Close), Is.EqualTo(new decimal?[] { 1.0001m, 1.0002m }));
    }

    [Test]
    public async Task Migration_creates_index_history_table()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await using var context = await database.ContextFactory.CreateDbContextAsync();

        var tableExists = await context.Database.SqlQueryRaw<bool>("""
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = current_schema()
                  AND table_name = 'MoexIndexHistories'
            ) AS "Value"
            """).SingleAsync();

        Assert.That(tableExists, Is.True);
    }

    private static MoexIndexHistory Point(string secId, DateOnly tradeDate, decimal close) => new()
    {
        SecId = secId,
        TradeDate = tradeDate,
        Close = close
    };
}
