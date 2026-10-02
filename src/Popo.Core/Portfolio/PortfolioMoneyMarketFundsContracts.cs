namespace Popo.Core.Portfolio;

public sealed record MoneyMarketFund(
    string SecId,
    string BoardId,
    double Quantity,
    double AveragePrice);

public sealed record MoneyMarketFundOperation(
    string SecId,
    DateOnly Date,
    TradeSide Side,
    double Quantity,
    double Price,
    double Commission);

public sealed record MoneyMarketFundOperationRecord(
    Guid Id,
    string SecId,
    DateOnly Date,
    TradeSide Side,
    double Quantity,
    double Price,
    double Commission,
    double Amount);

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
    Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(CancellationToken cancellationToken);
    Task<MoneyMarketFundRecord> AddMoneyMarketFundAsync(MoneyMarketFund fund, CancellationToken cancellationToken);
    Task<MoneyMarketFundRecord?> UpdateMoneyMarketFundAsync(Guid id, MoneyMarketFund fund, CancellationToken cancellationToken);
    Task<bool> DeleteMoneyMarketFundAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<MoneyMarketFundOperationRecord>> GetMoneyMarketFundOperationsAsync(CancellationToken cancellationToken);
    Task<MoneyMarketFundOperationRecord> AddMoneyMarketFundOperationAsync(
        MoneyMarketFundOperation operation,
        CancellationToken cancellationToken);
}
