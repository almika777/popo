using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using NUnit.Framework;
using Popo.Storage.Entities.Moex;
using Popo.Storage.Providers;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class DailyVolumeStatisticsTests
{
    [Test]
    public async Task Migration_BackfillsExistingHistory()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await using var db = await database.ContextFactory.CreateDbContextAsync();
        var migrator = db.GetService<IMigrator>();
        var migrations = db.Database.GetMigrations().ToArray();
        var dailyVolumeMigration = Array.FindIndex(
            migrations,
            x => x.EndsWith("_AddDailyVolumeStatistics", StringComparison.Ordinal));
        Assert.That(dailyVolumeMigration, Is.GreaterThan(0));
        await migrator.MigrateAsync(migrations[dailyVolumeMigration - 1]);
        var secId = $"migration-{Guid.NewGuid():N}";
        db.MoexHistoryYieldsEntities.Add(new MoexHistoryYieldsEntity
        {
            SecId = secId, BoardId = "A", TradeDate = new DateOnly(2026, 1, 1), Volume = 42
        });
        await db.SaveChangesAsync();
        await migrator.MigrateAsync();
        var value = await db.DailyVolumeStatistics.SingleAsync(x => x.SecId == secId);
        Assert.That(value.Average, Is.EqualTo(42));
        Assert.That(value.Median, Is.EqualTo(42));
    }

    [Test]
    public async Task Refresh_FailureAfterDelete_PreservesPublishedSnapshot()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var factory = database.ContextFactory;
        await using var db = await factory.CreateDbContextAsync();
        var secId = $"rollback-{Guid.NewGuid():N}";
        db.MoexHistoryYieldsEntities.Add(new MoexHistoryYieldsEntity
        {
            SecId = secId, BoardId = "A", TradeDate = new DateOnly(2026, 1, 1), Volume = 10
        });
        await db.SaveChangesAsync();
        var store = new DailyVolumeStatisticsStore(factory);
        await store.RefreshAsync(default);
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "DailyVolumeStatistics" ADD CONSTRAINT test_average CHECK ("Average" <= 10)
            """);
        await db.MoexHistoryYieldsEntities.Where(x => x.SecId == secId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Volume, 20d));
        Assert.ThrowsAsync<PostgresException>(() => store.RefreshAsync(default));
        var value = await db.DailyVolumeStatistics.AsNoTracking().SingleAsync(x => x.SecId == secId);
        Assert.That(value.Average, Is.EqualTo(10));
    }

    [Test]
    public async Task Refresh_PublishesLatestTwentyDatesAcrossBoards_AndReplacesSnapshot()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var factory = database.ContextFactory;
        var secId = $"volume-{Guid.NewGuid():N}";
        await using var db = await factory.CreateDbContextAsync();
        var start = new DateOnly(2026, 1, 1);
        for (var day = 0; day <= 20; day++)
            db.MoexHistoryYieldsEntities.Add(new MoexHistoryYieldsEntity
            {
                SecId = secId, BoardId = "A", TradeDate = start.AddDays(day * 2),
                Volume = day == 0 ? 10000 : day
            });
        db.MoexHistoryYieldsEntities.Add(new MoexHistoryYieldsEntity
        {
            SecId = secId, BoardId = "B", TradeDate = start.AddDays(40), Volume = 20
        });
        db.MoexHistoryYieldsEntities.Add(new MoexHistoryYieldsEntity
        {
            SecId = secId, BoardId = "A", TradeDate = start.AddDays(42), Volume = null
        });
        await db.SaveChangesAsync();

        var provider = new HistoryProvider(factory);
        var store = new DailyVolumeStatisticsStore(factory);
        Assert.That((await provider.GetDailyVolumeStatistics(default)).ContainsKey(secId), Is.False);
        await store.RefreshAsync(default);
        var value = (await provider.GetDailyVolumeStatistics(default))[secId];
        Assert.That(value.Average, Is.EqualTo(11.5));
        Assert.That(value.Median, Is.EqualTo(10.5));
        var snapshot = await db.DailyVolumeStatistics.AsNoTracking().SingleAsync(x => x.SecId == secId);
        Assert.That(snapshot.HistoryThroughDate, Is.EqualTo(start.AddDays(40)));
        Assert.That(snapshot.CalculatedAt, Is.GreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1)));

        await db.MoexHistoryYieldsEntities.Where(x => x.SecId == secId).ExecuteDeleteAsync();
        Assert.That((await provider.GetDailyVolumeStatistics(default))[secId].Average, Is.EqualTo(11.5));
        await store.RefreshAsync(default);
        Assert.That((await provider.GetDailyVolumeStatistics(default)).ContainsKey(secId), Is.False);
    }

    [Test]
    public async Task Refresh_KeepsZeroAndShortHistory_AndIsRepeatable()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var factory = database.ContextFactory;
        var secId = $"volume-{Guid.NewGuid():N}";
        await using var db = await factory.CreateDbContextAsync();
        foreach (var (day, volume) in new[] { (1, 0d), (2, 10d), (3, 50d) })
            db.MoexHistoryYieldsEntities.Add(new MoexHistoryYieldsEntity
            {
                SecId = secId, BoardId = "A", TradeDate = new DateOnly(2026, 1, day), Volume = volume
            });
        await db.SaveChangesAsync();
        var store = new DailyVolumeStatisticsStore(factory);
        await store.RefreshAsync(default);
        await store.RefreshAsync(default);
        var provider = new HistoryProvider(factory);
        var value = (await provider.GetDailyVolumeStatistics(default))[secId];
        Assert.That(value.Average, Is.EqualTo(20));
        Assert.That(value.Median, Is.EqualTo(10));
        Assert.That(await db.DailyVolumeStatistics.CountAsync(x => x.SecId == secId), Is.EqualTo(1));
    }
}
