namespace Popo.Core.Portfolio.Trades;

public interface IPortfolioTradesProvider
{
    Task<IReadOnlyList<PortfolioTradeRecord>> GetTradesAsync(CancellationToken cancellationToken);
    Task<PortfolioTradeRecord?> GetTradeAsync(Guid id, CancellationToken cancellationToken);
    Task<PortfolioTradeRecord> AddTradeAsync(PortfolioTrade trade, CancellationToken cancellationToken);
    Task<bool> UpdateTradeAsync(Guid id, PortfolioTrade trade, CancellationToken cancellationToken);
    Task<bool> DeleteTradeAsync(Guid id, CancellationToken cancellationToken);
}