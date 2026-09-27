using Microsoft.EntityFrameworkCore;
using Popo.Core.Portfolio;
using Popo.Storage.Entities;

namespace Popo.Storage.Providers.Portfolio;

public sealed class PortfolioCashProvider(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IPortfolioCashProvider
{
    public async Task<IReadOnlyList<CashSnapshotRecord>> GetCashSnapshotsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.CashSnapshots.AsNoTracking()
            .OrderByDescending(x => x.SnapshotDate).ThenBy(x => x.CurrencyId)
            .Select(x => new CashSnapshotRecord(x.Id, x.CurrencyId, x.SnapshotDate, x.Amount, x.Comment))
            .ToListAsync(cancellationToken);
    }

    public async Task<CashSnapshotRecord?> GetCashSnapshotAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.CashSnapshots.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new CashSnapshotRecord(x.Id, x.CurrencyId, x.SnapshotDate, x.Amount, x.Comment))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CashSnapshotRecord> AddCashSnapshotAsync(
        CashSnapshot snapshot,
        string comment,
        CancellationToken cancellationToken)
    {
        ValidateSnapshot(snapshot);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = new CashSnapshotEntity
        {
            Id = Guid.NewGuid(), CurrencyId = snapshot.CurrencyId, SnapshotDate = snapshot.SnapshotDate,
            Amount = snapshot.Amount, Comment = comment
        };
        dbContext.CashSnapshots.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToRecord(entity);
    }

    public async Task<CashSnapshotRecord?> UpdateCashSnapshotAsync(
        Guid id,
        CashSnapshot snapshot,
        string comment,
        CancellationToken cancellationToken)
    {
        ValidateSnapshot(snapshot);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.CashSnapshots.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.CurrencyId = snapshot.CurrencyId;
        entity.SnapshotDate = snapshot.SnapshotDate;
        entity.Amount = snapshot.Amount;
        entity.Comment = comment;
        dbContext.Attach(entity);
        dbContext.Entry(entity).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToRecord(entity);
    }

    public async Task<bool> DeleteCashSnapshotAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.CashSnapshots.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
    }

    private static CashSnapshotRecord ToRecord(CashSnapshotEntity entity) =>
        new(entity.Id, entity.CurrencyId, entity.SnapshotDate, entity.Amount, entity.Comment);

    private static void ValidateSnapshot(CashSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.CurrencyId) || !double.IsFinite(snapshot.Amount) || snapshot.Amount < 0)
        {
            throw new ArgumentException("Укажите валюту кеша и неотрицательную сумму.");
        }
    }
}
