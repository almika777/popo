using Microsoft.EntityFrameworkCore;
using Popo.Core.Portfolio;
using Popo.Storage.Entities;

namespace Popo.Storage.Providers.Portfolio;

public sealed class PortfolioMoneyMarketFundsProvider(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IPortfolioMoneyMarketFundsProvider
{
    public async Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var funds = await dbContext.MoneyMarketFunds.AsNoTracking()
            .OrderBy(x => x.SecId)
            .Select(x => new MoneyMarketFundRecord(x.Id, x.SecId, x.BoardId, x.Quantity, x.AveragePrice))
            .ToListAsync(cancellationToken);
        var operations = await GetMoneyMarketFundOperationInputsAsync(cancellationToken);
        return funds.Select(fund =>
        {
            var position = MoneyMarketFundOperationCalculator.CalculatePosition(
                new MoneyMarketFund(fund.SecId, fund.BoardId, fund.Quantity, fund.AveragePrice),
                operations.Where(operation => string.Equals(
                    operation.SecId,
                    fund.SecId,
                    StringComparison.OrdinalIgnoreCase)));
            return fund with { Quantity = position.Quantity, AveragePrice = position.AveragePrice };
        }).ToArray();
    }

    public async Task<IReadOnlyList<MoneyMarketFundOperationRecord>> GetMoneyMarketFundOperationsAsync(
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.MoneyMarketFundOperations.AsNoTracking()
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
            .Select(x => new MoneyMarketFundOperationRecord(
                x.Id, x.SecId, x.Date, x.Side, x.Quantity, x.Price, x.Commission,
                x.Quantity * x.Price))
            .ToListAsync(cancellationToken);
    }

    public async Task<MoneyMarketFundOperationRecord> AddMoneyMarketFundOperationAsync(
        MoneyMarketFundOperation operation,
        CancellationToken cancellationToken)
    {
        ValidateMoneyMarketFundOperation(operation);
        var funds = await GetMoneyMarketFundsAsync(cancellationToken);
        var fund = funds.SingleOrDefault(x => string.Equals(
                x.SecId,
                operation.SecId,
                StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Начальный остаток фонда денежного рынка не найден.");
        if (operation.Side == TradeSide.Sell && operation.Quantity > fund.Quantity)
        {
            throw new InvalidOperationException("Нельзя продать больше паёв фонда, чем есть в наличии.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = new MoneyMarketFundOperationEntity
        {
            Id = Guid.NewGuid(), SecId = operation.SecId.Trim().ToUpperInvariant(), Date = operation.Date,
            Side = operation.Side, Quantity = operation.Quantity, Price = operation.Price, Commission = operation.Commission
        };
        dbContext.MoneyMarketFundOperations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new MoneyMarketFundOperationRecord(entity.Id, entity.SecId, entity.Date, entity.Side,
            entity.Quantity, entity.Price, entity.Commission, entity.Quantity * entity.Price);
    }

    public async Task<MoneyMarketFundRecord> AddMoneyMarketFundAsync(
        MoneyMarketFund fund,
        CancellationToken cancellationToken)
    {
        ValidateMoneyMarketFund(fund);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = new MoneyMarketFundEntity
        {
            Id = Guid.NewGuid(), SecId = fund.SecId.Trim().ToUpperInvariant(), BoardId = fund.BoardId.Trim(),
            Quantity = fund.Quantity, AveragePrice = fund.AveragePrice
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
        {
            return null;
        }

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

    private async Task<IReadOnlyList<MoneyMarketFundOperation>> GetMoneyMarketFundOperationInputsAsync(
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.MoneyMarketFundOperations.AsNoTracking()
            .OrderBy(x => x.Date).ThenBy(x => x.Id)
            .Select(x => new MoneyMarketFundOperation(x.SecId, x.Date, x.Side, x.Quantity, x.Price, x.Commission))
            .ToListAsync(cancellationToken);
    }

    private static MoneyMarketFundRecord ToRecord(MoneyMarketFundEntity entity) =>
        new(entity.Id, entity.SecId, entity.BoardId, entity.Quantity, entity.AveragePrice);

    private static void ValidateMoneyMarketFund(MoneyMarketFund fund)
    {
        if (string.IsNullOrWhiteSpace(fund.SecId) || string.IsNullOrWhiteSpace(fund.BoardId)
            || !double.IsFinite(fund.Quantity) || fund.Quantity <= 0 || fund.Quantity != Math.Truncate(fund.Quantity)
            || !double.IsFinite(fund.AveragePrice) || fund.AveragePrice <= 0)
        {
            throw new ArgumentException("Укажите фонд, торговую площадку, количество и среднюю цену.");
        }
    }

    private static void ValidateMoneyMarketFundOperation(MoneyMarketFundOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.SecId)
            || !double.IsFinite(operation.Quantity) || operation.Quantity <= 0 || operation.Quantity != Math.Truncate(operation.Quantity)
            || !double.IsFinite(operation.Price) || operation.Price <= 0
            || !double.IsFinite(operation.Commission) || operation.Commission < 0)
        {
            throw new ArgumentException("Проверьте фонд, количество, цену и комиссию.");
        }
    }
}
