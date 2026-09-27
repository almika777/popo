using Microsoft.EntityFrameworkCore;
using Popo.Core.PortfolioReturns;
using Popo.Storage.Entities;

namespace Popo.Storage.Providers.PortfolioReturns;

public sealed class PortfolioValuationsProvider(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IPortfolioValuationsProvider
{
    public async Task<IReadOnlyList<PortfolioValuationRecord>> GetValuationsAsync(
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.PortfolioValuations
            .AsNoTracking()
            .OrderByDescending(x => x.Date)
            .Select(x => new PortfolioValuationRecord(x.Id, x.Date, x.TotalValue, x.Comment))
            .ToListAsync(cancellationToken);
    }

    public async Task<PortfolioValuationRecord?> GetValuationAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.PortfolioValuations
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new PortfolioValuationRecord(x.Id, x.Date, x.TotalValue, x.Comment))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PortfolioValuationRecord> AddValuationAsync(
        DateOnly date,
        double totalValue,
        string comment,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = new PortfolioValuationEntity
        {
            Id = Guid.NewGuid(),
            Date = date,
            TotalValue = totalValue,
            Comment = comment
        };
        dbContext.PortfolioValuations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToRecord(entity);
    }

    public async Task<bool> UpdateValuationAsync(
        Guid id,
        DateOnly date,
        double totalValue,
        string comment,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var updated = await dbContext.PortfolioValuations
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Date, date)
                .SetProperty(x => x.TotalValue, totalValue)
                .SetProperty(x => x.Comment, comment), cancellationToken);
        return updated > 0;
    }

    public async Task<bool> DeleteValuationAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var deleted = await dbContext.PortfolioValuations.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    private static PortfolioValuationRecord ToRecord(PortfolioValuationEntity entity) =>
        new(entity.Id, entity.Date, entity.TotalValue, entity.Comment);
}
