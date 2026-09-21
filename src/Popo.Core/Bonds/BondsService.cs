namespace Popo.Core.Bonds;

public sealed class BondsService(IBondsProvider bondsProvider) : IBondsService
{
    public Task<IReadOnlyList<BondSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
        => bondsProvider.SearchAsync(query, cancellationToken);

    public Task<IReadOnlyList<string>> GetTradingCurrenciesAsync(CancellationToken cancellationToken)
        => bondsProvider.GetTradingCurrenciesAsync(cancellationToken);

    public Task<IReadOnlyList<string>> GetFaceUnitsAsync(CancellationToken cancellationToken)
        => bondsProvider.GetFaceUnitsAsync(cancellationToken);
}
