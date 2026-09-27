using Microsoft.EntityFrameworkCore;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Trades;
using Popo.Storage.Entities;

namespace Popo.Storage.Providers.Portfolio;

public class PortfolioTradesProvider(IDbContextFactory<PopoDbContext> dbContextFactory) : IPortfolioTradesProvider
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
}