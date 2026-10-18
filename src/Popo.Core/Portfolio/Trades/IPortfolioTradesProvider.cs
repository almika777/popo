namespace Popo.Core.Portfolio.Trades;

public interface IPortfolioTradesProvider
{
    /// <summary>Возвращает все сохранённые сделки; отбор по дате расчёта выполняется калькулятором остатков.</summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Сохранённые сделки портфеля.</returns>
    Task<IReadOnlyList<PortfolioTradeRecord>> GetTradesAsync(CancellationToken cancellationToken);
    Task<PortfolioTradeRecord?> GetTradeAsync(Guid id, CancellationToken cancellationToken);
    Task<PortfolioTradeRecord> AddTradeAsync(PortfolioTrade trade, CancellationToken cancellationToken);
    Task<bool> UpdateTradeAsync(Guid id, PortfolioTrade trade, CancellationToken cancellationToken);
    Task<bool> DeleteTradeAsync(Guid id, CancellationToken cancellationToken);
}
