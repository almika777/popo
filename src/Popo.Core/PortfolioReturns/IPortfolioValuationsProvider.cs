namespace Popo.Core.PortfolioReturns;

public interface IPortfolioValuationsProvider
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
}
