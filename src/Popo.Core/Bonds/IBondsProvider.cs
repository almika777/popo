namespace Popo.Core.Bonds;

public interface IBondsProvider
{
    Task<IReadOnlyList<BondSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BondFaceValueResult>> GetHistoricalFaceValuesAsync(
        IReadOnlyList<BondFaceValueRequest> requests,
        CancellationToken cancellationToken);

    /// <summary>Читает уникальные коды валют из сохранённого списка облигаций MOEX.</summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Коды валют, указанные в данных облигаций.</returns>
    Task<IReadOnlyList<string>> GetTradingCurrenciesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetFaceUnitsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<BondSecurityDto>> GetSecurities(CancellationToken cancellationToken);
}
