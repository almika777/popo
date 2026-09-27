namespace Popo.Core.Portfolio.Position;

public interface IPortfolioPositionsProvider
{
    Task<IReadOnlyList<PortfolioPositionRecord>> GetPositionsAsync(DateOnly asOf, CancellationToken cancellationToken);
}
