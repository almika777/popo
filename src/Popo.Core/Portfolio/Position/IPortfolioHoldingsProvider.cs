namespace Popo.Core.Portfolio.Position;

public interface IPortfolioHoldingsProvider
{
    Task<IReadOnlyList<PortfolioHoldingRecord>> GetHoldingsAsync(DateOnly asOf, CancellationToken cancellationToken);
}
