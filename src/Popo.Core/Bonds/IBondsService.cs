namespace Popo.Core.Bonds;

public interface IBondsService
{
    Task<IReadOnlyList<BondSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BondFaceValueResult>> GetHistoricalFaceValuesAsync(
        IReadOnlyList<BondFaceValueRequest> requests,
        CancellationToken cancellationToken);

    /// <summary>Возвращает валюты торгов облигациями, используемые для фильтрации рекомендаций.</summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Уникальные коды валют торгов.</returns>
    Task<IReadOnlyList<string>> GetTradingCurrenciesAsync(CancellationToken cancellationToken);

    /// <summary>Возвращает валюты номинала.</summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Уникальные коды валют номинала.</returns>
    Task<IReadOnlyList<string>> GetFaceUnitsAsync(CancellationToken cancellationToken);
}
