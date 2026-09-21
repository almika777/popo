namespace Popo.Core.PortfolioReturns;

public interface IPortfolioReturnInputsProvider
{
    Task<IReadOnlyList<PortfolioValuationRecord>> GetValuationsAsync(CancellationToken cancellationToken);

    Task<PortfolioValuationRecord?> GetValuationAsync(Guid id, CancellationToken cancellationToken);

    Task<PortfolioValuationRecord> AddValuationAsync(
        DateOnly date,
        double totalValue,
        string comment,
        CancellationToken cancellationToken);

    Task<bool> UpdateValuationAsync(
        Guid id,
        DateOnly date,
        double totalValue,
        string comment,
        CancellationToken cancellationToken);

    Task<bool> DeleteValuationAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<PortfolioCashFlowRecord>> GetCashFlowsAsync(CancellationToken cancellationToken);

    Task<PortfolioCashFlowRecord?> GetCashFlowAsync(Guid id, CancellationToken cancellationToken);

    Task<PortfolioCashFlowRecord> AddCashFlowAsync(
        DateOnly date,
        PortfolioCashFlowType type,
        double amount,
        string comment,
        CancellationToken cancellationToken);

    Task<bool> UpdateCashFlowAsync(
        Guid id,
        DateOnly date,
        PortfolioCashFlowType type,
        double amount,
        string comment,
        CancellationToken cancellationToken);

    Task<bool> DeleteCashFlowAsync(Guid id, CancellationToken cancellationToken);
}
