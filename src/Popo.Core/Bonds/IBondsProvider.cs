namespace Popo.Core.Bonds;

public interface IBondsProvider
{
    Task<IReadOnlyList<BondSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetTradingCurrenciesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetFaceUnitsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<BondSecurityDto>> GetSecurities(CancellationToken cancellationToken);
}
