namespace Popo.Core.Portfolio;

public sealed record MoneyMarketFund(
    string SecId,
    string BoardId,
    double Quantity,
    double AveragePrice);

public sealed record MoneyMarketFundRecord(
    Guid Id,
    string SecId,
    string BoardId,
    double Quantity,
    double AveragePrice,
    double? CurrentPrice = null,
    double? CurrentValue = null,
    double? Pnl = null,
    double? PnlPercent = null,
    DateTime? QuoteTime = null);

public interface IPortfolioMoneyMarketFundsProvider
{
    /// <summary>Возвращает сохранённые текущие позиции фондов с количеством и средней ценой.</summary>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список позиций фондов денежного рынка.</returns>
    Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(CancellationToken cancellationToken);
    Task<MoneyMarketFundRecord> AddMoneyMarketFundAsync(MoneyMarketFund fund, CancellationToken cancellationToken);
    Task<MoneyMarketFundRecord?> UpdateMoneyMarketFundAsync(Guid id, MoneyMarketFund fund, CancellationToken cancellationToken);
    Task<bool> DeleteMoneyMarketFundAsync(Guid id, CancellationToken cancellationToken);
}
