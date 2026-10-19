using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;
using Popo.Storage;
using Popo.Storage.Entities;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class PortfolioMoneyMarketFundMigrationTests
{
    [Test]
    public async Task Migration_FoldsLegacyOperationsAndPreservesArchiveAcrossRollbackAndReapply()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await using var db = await database.ContextFactory.CreateDbContextAsync();
        var migrator = db.GetService<IMigrator>();
        var baselineMigration = db.Database.GetMigrations().Single(
            x => x.EndsWith("_RemoveCashSnapshots", StringComparison.Ordinal));
        await migrator.MigrateAsync(baselineMigration);

        var fundId = Guid.NewGuid();
        var unchangedFundId = Guid.NewGuid();
        db.MoneyMarketFunds.AddRange(
            new MoneyMarketFundEntity
            {
                Id = fundId, SecId = "LQDT", BoardId = "TQBR", Quantity = 100, AveragePrice = 10
            },
            new MoneyMarketFundEntity
            {
                Id = unchangedFundId, SecId = "TMON", BoardId = "TQBR", Quantity = 20, AveragePrice = 100
            });
        await db.SaveChangesAsync();
        var buyOperationId = Guid.NewGuid();
        await InsertLegacyOperationAsync(db, buyOperationId, "LQDT", new DateOnly(2026, 1, 2), 0, 20, 15, 5);
        await InsertLegacyOperationAsync(db, Guid.NewGuid(), "LQDT", new DateOnly(2026, 1, 3), 1, 10, 12, 1);

        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();

        var migratedFunds = await db.MoneyMarketFunds.AsNoTracking().ToDictionaryAsync(x => x.Id);
        Assert.Multiple(() =>
        {
            Assert.That(migratedFunds[fundId].Quantity, Is.EqualTo(110));
            Assert.That(migratedFunds[fundId].AveragePrice, Is.EqualTo(10.875).Within(0.000001));
            Assert.That(migratedFunds[unchangedFundId].Quantity, Is.EqualTo(20));
            Assert.That(migratedFunds[unchangedFundId].AveragePrice, Is.EqualTo(100));
        });
        Assert.That(await ReadCountAsync(db, "ArchivedMoneyMarketFunds"), Is.EqualTo(2));
        Assert.That(await ReadCountAsync(db, "ArchivedMoneyMarketFundOperations"), Is.EqualTo(2));
        Assert.That(await ReadDoubleAsync(db,
            "SELECT \"Quantity\" FROM \"ArchivedMoneyMarketFunds\" WHERE \"Id\" = @id", fundId), Is.EqualTo(100));
        Assert.That(await ReadDoubleAsync(db,
            "SELECT \"Commission\" FROM \"ArchivedMoneyMarketFundOperations\" WHERE \"Id\" = @id", buyOperationId), Is.EqualTo(5));

        await migrator.MigrateAsync(baselineMigration);
        db.ChangeTracker.Clear();
        var rolledBackFunds = await db.MoneyMarketFunds.AsNoTracking().ToDictionaryAsync(x => x.Id);
        Assert.Multiple(() =>
        {
            Assert.That(rolledBackFunds[fundId].Quantity, Is.EqualTo(100));
            Assert.That(rolledBackFunds[fundId].AveragePrice, Is.EqualTo(10));
        });
        Assert.That(await ReadCountAsync(db, "MoneyMarketFundOperations"), Is.EqualTo(2));

        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();
        var reappliedFund = await db.MoneyMarketFunds.AsNoTracking().SingleAsync(x => x.Id == fundId);

        Assert.Multiple(() =>
        {
            Assert.That(reappliedFund.Quantity, Is.EqualTo(110));
            Assert.That(reappliedFund.AveragePrice, Is.EqualTo(10.875).Within(0.000001));
        });
        Assert.That(await ReadCountAsync(db, "ArchivedMoneyMarketFunds"), Is.EqualTo(2));
        Assert.That(await ReadCountAsync(db, "ArchivedMoneyMarketFundOperations"), Is.EqualTo(2));
    }

    [Test]
    public async Task Migration_ReappliesAfterFundIsRecreatedWithSameBusinessKey()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await using var db = await database.ContextFactory.CreateDbContextAsync();
        var migrator = db.GetService<IMigrator>();
        var baselineMigration = db.Database.GetMigrations().Single(
            x => x.EndsWith("_RemoveCashSnapshots", StringComparison.Ordinal));
        await migrator.MigrateAsync(baselineMigration);

        var originalFundId = Guid.NewGuid();
        db.MoneyMarketFunds.Add(new MoneyMarketFundEntity
        {
            Id = originalFundId, SecId = "LQDT", BoardId = "TQBR", Quantity = 100, AveragePrice = 10
        });
        await db.SaveChangesAsync();
        await InsertLegacyOperationAsync(
            db, Guid.NewGuid(), "LQDT", new DateOnly(2026, 1, 2), 0, 10, 15, 5);

        await migrator.MigrateAsync();
        await migrator.MigrateAsync(baselineMigration);
        db.ChangeTracker.Clear();

        await db.MoneyMarketFunds.Where(x => x.Id == originalFundId).ExecuteDeleteAsync();
        var replacementFundId = Guid.NewGuid();
        db.MoneyMarketFunds.Add(new MoneyMarketFundEntity
        {
            Id = replacementFundId, SecId = "LQDT", BoardId = "TQBR", Quantity = 200, AveragePrice = 20
        });
        await db.SaveChangesAsync();

        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();

        var migratedReplacement = await db.MoneyMarketFunds.AsNoTracking()
            .SingleAsync(x => x.Id == replacementFundId);
        Assert.Multiple(() =>
        {
            Assert.That(migratedReplacement.Quantity, Is.EqualTo(210));
            Assert.That(migratedReplacement.AveragePrice, Is.EqualTo(19.785714285714285).Within(0.000001));
        });
        var migratedReplacementQuantity = migratedReplacement.Quantity;
        var migratedReplacementAveragePrice = migratedReplacement.AveragePrice;
        Assert.That(await ReadCountAsync(db, "ArchivedMoneyMarketFunds"), Is.EqualTo(1));
        Assert.That(await ReadCountAsync(db, "ArchivedMoneyMarketFundOperations"), Is.EqualTo(1));
        Assert.That(await ReadDoubleAsync(db,
            "SELECT \"Quantity\" FROM \"ArchivedMoneyMarketFunds\" WHERE \"Id\" = @id", originalFundId),
            Is.EqualTo(100));

        await migrator.MigrateAsync(baselineMigration);
        db.ChangeTracker.Clear();
        var rolledBackReplacement = await db.MoneyMarketFunds.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == replacementFundId);
        Assert.That(rolledBackReplacement, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(rolledBackReplacement!.Quantity, Is.EqualTo(migratedReplacementQuantity));
            Assert.That(rolledBackReplacement.AveragePrice, Is.EqualTo(migratedReplacementAveragePrice));
        });
        Assert.That(await ReadCountAsync(db, "MoneyMarketFundOperations"), Is.Zero);

        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();
        var reappliedReplacement = await db.MoneyMarketFunds.AsNoTracking()
            .SingleAsync(x => x.Id == replacementFundId);
        Assert.Multiple(() =>
        {
            Assert.That(reappliedReplacement.Quantity, Is.EqualTo(migratedReplacementQuantity));
            Assert.That(reappliedReplacement.AveragePrice, Is.EqualTo(migratedReplacementAveragePrice));
        });
        Assert.That(await ReadCountAsync(db, "ArchivedMoneyMarketFunds"), Is.EqualTo(1));
        Assert.That(await ReadCountAsync(db, "ArchivedMoneyMarketFundOperations"), Is.EqualTo(1));
    }

    private static Task<int> InsertLegacyOperationAsync(
        PopoDbContext db,
        Guid id,
        string secId,
        DateOnly date,
        int side,
        double quantity,
        double price,
        double commission) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "MoneyMarketFundOperations"
                ("Id", "SecId", "Date", "Side", "Quantity", "Price", "Commission")
            VALUES
                ({id}, {secId}, {date}, {side}, {quantity}, {price}, {commission})
            """);

    private static async Task<int> ReadCountAsync(PopoDbContext db, string tableName)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{tableName}\"";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<double> ReadDoubleAsync(PopoDbContext db, string query, Guid id)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = query;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "id";
        parameter.Value = id;
        command.Parameters.Add(parameter);
        return Convert.ToDouble(await command.ExecuteScalarAsync());
    }
}
