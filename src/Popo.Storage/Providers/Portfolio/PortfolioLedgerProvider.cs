using Microsoft.EntityFrameworkCore;
using Popo.Core.Portfolio;
using Popo.Storage.Entities;

namespace Popo.Storage.Providers.Portfolio;

public sealed class PortfolioLedgerProvider(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IPortfolioLedgerProvider
{
    public async Task<IReadOnlyList<PortfolioTradeRecord>> GetTradesAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.PortfolioTrades.AsNoTracking()
            .OrderByDescending(x => x.TradeDate).ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        return entities.Select(ToRecord).ToArray();
    }

    public async Task<PortfolioTradeRecord?> GetTradeAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.PortfolioTrades.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<PortfolioTradeRecord> AddTradeAsync(PortfolioTrade trade, CancellationToken cancellationToken)
    {
        ValidateTrade(trade);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = ToEntity(trade);
        dbContext.PortfolioTrades.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToRecord(entity);
    }

    public async Task<bool> UpdateTradeAsync(Guid id, PortfolioTrade trade, CancellationToken cancellationToken)
    {
        ValidateTrade(trade);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.PortfolioTrades.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.SecId = trade.SecId;
        entity.BoardId = trade.BoardId;
        entity.CurrencyId = trade.CurrencyId;
        entity.TradeDate = trade.TradeDate;
        entity.Side = trade.Side;
        entity.Quantity = trade.Quantity;
        entity.Price = trade.Price;
        entity.FaceValue = trade.FaceValue;
        entity.AccruedInterest = trade.AccruedInterest;
        entity.Commission = trade.CommissionAmount;
        dbContext.Attach(entity);
        dbContext.Entry(entity).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteTradeAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.PortfolioTrades.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<PortfolioPositionRecord>> GetPositionsAsync(CancellationToken cancellationToken)
    {
        var trades = await GetTradeInputsAsync(cancellationToken);
        return PortfolioPositionCalculator.Calculate(trades)
            .Select(PortfolioPositionCalculator.ToRecord)
            .ToArray();
    }

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

    public async Task<CashSnapshotRecord> AddCashSnapshotAsync(CashSnapshot snapshot, string comment, CancellationToken cancellationToken)
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

    public async Task<CashSnapshotRecord?> UpdateCashSnapshotAsync(Guid id, CashSnapshot snapshot, string comment, CancellationToken cancellationToken)
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

    public async Task<IReadOnlyList<CashBalance>> GetCashBalancesAsync(DateOnly asOf, CancellationToken cancellationToken)
    {
        var snapshots = await GetCashSnapshotInputsAsync(cancellationToken);
        var trades = await GetTradeInputsAsync(cancellationToken);
        var fundOperations = await GetMoneyMarketFundOperationInputsAsync(cancellationToken);
        return PortfolioCashCalculator.Calculate(snapshots, trades, asOf, fundOperations);
    }

    public async Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(CancellationToken cancellationToken)
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
                operations.Where(operation => string.Equals(operation.SecId, fund.SecId, StringComparison.OrdinalIgnoreCase)));
            return fund with { Quantity = position.Quantity, AveragePrice = position.AveragePrice };
        }).ToArray();
    }

    public async Task<IReadOnlyList<MoneyMarketFundOperationRecord>> GetMoneyMarketFundOperationsAsync(CancellationToken cancellationToken)
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
        var fund = funds.SingleOrDefault(x => string.Equals(x.SecId, operation.SecId, StringComparison.OrdinalIgnoreCase))
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

    public async Task<MoneyMarketFundRecord> AddMoneyMarketFundAsync(MoneyMarketFund fund, CancellationToken cancellationToken)
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

    public async Task<MoneyMarketFundRecord?> UpdateMoneyMarketFundAsync(Guid id, MoneyMarketFund fund, CancellationToken cancellationToken)
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

    private async Task<IReadOnlyList<PortfolioTrade>> GetTradeInputsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.PortfolioTrades.AsNoTracking()
            .Select(x => new PortfolioTrade(x.SecId, x.BoardId, x.CurrencyId, x.TradeDate, x.Side, x.Quantity, x.Price, x.FaceValue, x.AccruedInterest,
                x.Commission / (x.Quantity * x.Price + x.Quantity * x.AccruedInterest) * 100))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<CashSnapshot>> GetCashSnapshotInputsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.CashSnapshots.AsNoTracking()
            .Select(x => new CashSnapshot(x.CurrencyId, x.SnapshotDate, x.Amount))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<MoneyMarketFundOperation>> GetMoneyMarketFundOperationInputsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.MoneyMarketFundOperations.AsNoTracking()
            .OrderBy(x => x.Date).ThenBy(x => x.Id)
            .Select(x => new MoneyMarketFundOperation(x.SecId, x.Date, x.Side, x.Quantity, x.Price, x.Commission))
            .ToListAsync(cancellationToken);
    }

    private static PortfolioTradeEntity ToEntity(PortfolioTrade trade) => new()
    {
        Id = Guid.NewGuid(), SecId = trade.SecId, BoardId = trade.BoardId, CurrencyId = trade.CurrencyId,
        TradeDate = trade.TradeDate, Side = trade.Side, Quantity = trade.Quantity, Price = trade.Price,
        FaceValue = trade.FaceValue, AccruedInterest = trade.AccruedInterest, Commission = trade.CommissionAmount
    };

    private static PortfolioTradeRecord ToRecord(PortfolioTradeEntity entity) =>
        new(entity.Id, entity.SecId, entity.BoardId, entity.CurrencyId, entity.TradeDate, entity.Side,
            entity.Quantity, entity.Price, entity.FaceValue, entity.AccruedInterest, entity.Commission,
            GetCommissionPercent(entity),
            entity.Side == TradeSide.Buy
                ? GetGrossAmount(entity) + entity.Commission
                : GetGrossAmount(entity) - entity.Commission);

    private static CashSnapshotRecord ToRecord(CashSnapshotEntity entity) =>
        new(entity.Id, entity.CurrencyId, entity.SnapshotDate, entity.Amount, entity.Comment);

    private static MoneyMarketFundRecord ToRecord(MoneyMarketFundEntity entity) =>
        new(entity.Id, entity.SecId, entity.BoardId, entity.Quantity, entity.AveragePrice);

    private static double GetGrossAmount(PortfolioTradeEntity entity) =>
        entity.Quantity * (entity.Price + entity.AccruedInterest);

    private static double GetCommissionPercent(PortfolioTradeEntity entity)
    {
        var grossAmount = GetGrossAmount(entity);
        return grossAmount == 0 ? 0 : entity.Commission / grossAmount * 100;
    }

    private static void ValidateTrade(PortfolioTrade trade)
    {
        if (string.IsNullOrWhiteSpace(trade.SecId) || string.IsNullOrWhiteSpace(trade.BoardId)
            || string.IsNullOrWhiteSpace(trade.CurrencyId) || !double.IsFinite(trade.Quantity) || trade.Quantity <= 0 || trade.Quantity != Math.Truncate(trade.Quantity)
            || !double.IsFinite(trade.Price) || trade.Price <= 0
            || !double.IsFinite(trade.FaceValue) || trade.FaceValue <= 0
            || !double.IsFinite(trade.AccruedInterest) || trade.AccruedInterest < 0
            || !double.IsFinite(trade.CommissionPercent) || trade.CommissionPercent < 0)
        {
            throw new ArgumentException("Укажите бумагу, торговую площадку, валюту, количество и цену сделки.");
        }
    }

    private static void ValidateSnapshot(CashSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.CurrencyId) || !double.IsFinite(snapshot.Amount) || snapshot.Amount < 0)
        {
            throw new ArgumentException("Укажите валюту кеша и неотрицательную сумму.");
        }
    }

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
