using Microsoft.EntityFrameworkCore;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;

namespace Popo.Storage.Providers.Portfolio;

public class PortfolioPositionsProvider(IDbContextFactory<PopoDbContext> dbContextFactory) : IPortfolioPositionsProvider
{
    public async Task<IReadOnlyList<PortfolioPositionRecord>> GetPositionsAsync(
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var trades = await GetTradeInputsAsync(asOf, cancellationToken);
        return PortfolioPositionCalculator.Calculate(trades)
            .Select(PortfolioPositionCalculator.ToRecord)
            .ToArray();
    }

    private async Task<IReadOnlyList<PortfolioTrade>> GetTradeInputsAsync(
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.PortfolioTrades.AsNoTracking()
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
                x.Commission / (x.Quantity * x.Price + x.Quantity * x.AccruedInterest) * 100)
            )
            .ToListAsync(cancellationToken);
    }
}
