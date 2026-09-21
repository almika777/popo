namespace Popo.Core.Bonds;

public interface IBondsService
{
    Task<IReadOnlyList<BondSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetTradingCurrenciesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetFaceUnitsAsync(CancellationToken cancellationToken);
}
