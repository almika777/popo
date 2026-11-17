using Microsoft.EntityFrameworkCore;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;

namespace Popo.Storage.Providers.Portfolio;

public class PortfolioHoldingsProvider(IDbContextFactory<PopoDbContext> dbContextFactory) : IPortfolioHoldingsProvider
{
    public async Task<IReadOnlyList<PortfolioHoldingRecord>> GetHoldingsAsync(
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var trades = await GetTradeInputsAsync(asOf, cancellationToken);
        PortfolioPositionsValidate.ValidateTradeSequence(trades);
        return trades.GroupBy(trade => (trade.SecId, trade.BoardId, trade.CurrencyId))
            .Select(group => new PortfolioHoldingRecord(group.Key.SecId, group.Key.BoardId, group.Key.CurrencyId,
                group.Sum(trade => trade.Side == TradeSide.Buy ? trade.Quantity : -trade.Quantity)))
            .Where(holding => holding.Quantity > 0)
            .OrderBy(holding => holding.SecId)
            .ThenBy(holding => holding.BoardId)
            .ThenBy(holding => holding.CurrencyId)
            .ToArray();
    }

    private async Task<IReadOnlyList<PortfolioTrade>> GetTradeInputsAsync(
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.PortfolioTrades
            .Where(x => x.TradeDate <= asOf)
            .Select(x => new PortfolioTrade(
                x.SecId,
                x.BoardId,
                x.CurrencyId, 
                x.TradeDate, 
                x.Side, 
                x.Quantity, 
                x.Price, 
                x.FaceValue, 
                x.AccruedInterest,
                x.Commission / (x.Quantity * x.Price + x.Quantity * x.AccruedInterest) * 100) { Id = x.Id }
            ).ToListAsync(cancellationToken);
    }
}
