using Microsoft.EntityFrameworkCore;
using Popo.Core.Portfolio;
using Popo.Storage.Entities;

namespace Popo.Storage.Providers.Portfolio;

public sealed class PortfolioMoneyMarketFundsProvider(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IPortfolioMoneyMarketFundsProvider
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.MoneyMarketFunds.AsNoTracking()
            .OrderBy(x => x.SecId)
            .Select(x => new MoneyMarketFundRecord(x.Id, x.SecId, x.BoardId, x.Quantity, x.AveragePrice))
            .ToListAsync(cancellationToken);
    }

    public async Task<MoneyMarketFundRecord> AddMoneyMarketFundAsync(
        MoneyMarketFund fund,
        CancellationToken cancellationToken)
    {
        ValidateMoneyMarketFund(fund);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = new MoneyMarketFundEntity
        {
            Id = Guid.CreateVersion7(),
            SecId = fund.SecId.Trim().ToUpperInvariant(),
            BoardId = fund.BoardId.Trim(),
            Quantity = fund.Quantity,
            AveragePrice = fund.AveragePrice
        };
        dbContext.MoneyMarketFunds.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToRecord(entity);
    }

    public async Task<MoneyMarketFundRecord?> UpdateMoneyMarketFundAsync(
        Guid id,
        MoneyMarketFund fund,
        CancellationToken cancellationToken)
    {
        ValidateMoneyMarketFund(fund);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.MoneyMarketFunds.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return null;
        
        entity.SecId = fund.SecId.Trim().ToUpperInvariant();
        entity.BoardId = fund.BoardId.Trim();
        entity.Quantity = fund.Quantity;
        entity.AveragePrice = fund.AveragePrice;
        dbContext.Attach(entity);
        dbContext.Entry(entity).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToRecord(entity);
    }

    public async Task<bool> DeleteMoneyMarketFundAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.MoneyMarketFunds.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
    }

    private static MoneyMarketFundRecord ToRecord(MoneyMarketFundEntity entity) =>
        new(entity.Id, entity.SecId, entity.BoardId, entity.Quantity, entity.AveragePrice);

    private static void ValidateMoneyMarketFund(MoneyMarketFund fund)
    {
        if (string.IsNullOrWhiteSpace(fund.SecId) || string.IsNullOrWhiteSpace(fund.BoardId)
                                                  || !double.IsFinite(fund.Quantity) || fund.Quantity <= 0 ||
                                                  fund.Quantity != Math.Truncate(fund.Quantity)
                                                  || !double.IsFinite(fund.AveragePrice) || fund.AveragePrice <= 0)
        {
            throw new ArgumentException("Укажите фонд, торговую площадку, количество и среднюю цену.");
        }
    }
}