using Microsoft.EntityFrameworkCore;
using Popo.Core.PortfolioReturns;
using Popo.Storage.Entities;

namespace Popo.Storage.Providers.PortfolioReturns;

public sealed class PortfolioReturnInputsProvider(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IPortfolioReturnInputsProvider
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

    public async Task<IReadOnlyList<PortfolioCashFlowRecord>> GetCashFlowsAsync(
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.PortfolioCashFlows
            .AsNoTracking()
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .Select(x => new PortfolioCashFlowRecord(x.Id, x.Date, x.Type, x.Amount, x.Comment))
            .ToListAsync(cancellationToken);
    }

    public async Task<PortfolioCashFlowRecord?> GetCashFlowAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.PortfolioCashFlows
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new PortfolioCashFlowRecord(x.Id, x.Date, x.Type, x.Amount, x.Comment))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PortfolioCashFlowRecord> AddCashFlowAsync(
        DateOnly date,
        PortfolioCashFlowType type,
        double amount,
        string comment,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = new PortfolioCashFlowEntity
        {
            Id = Guid.NewGuid(),
            Date = date,
            Type = type,
            Amount = amount,
            Comment = comment
        };
        dbContext.PortfolioCashFlows.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToRecord(entity);
    }

    public async Task<bool> UpdateCashFlowAsync(
        Guid id,
        DateOnly date,
        PortfolioCashFlowType type,
        double amount,
        string comment,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.PortfolioCashFlows.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.Date = date;
        entity.Type = type;
        entity.Amount = amount;
        entity.Comment = comment;
        dbContext.Attach(entity);
        dbContext.Entry(entity).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteCashFlowAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var deleted = await dbContext.PortfolioCashFlows.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    private static PortfolioValuationRecord ToRecord(PortfolioValuationEntity entity) =>
        new(entity.Id, entity.Date, entity.TotalValue, entity.Comment);

    private static PortfolioCashFlowRecord ToRecord(PortfolioCashFlowEntity entity) =>
        new(entity.Id, entity.Date, entity.Type, entity.Amount, entity.Comment);
}
