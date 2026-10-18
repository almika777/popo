namespace Popo.Core.Bonds;

public interface IBondsService
{
    Task<IReadOnlyList<BondSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BondFaceValueResult>> GetHistoricalFaceValuesAsync(
        IReadOnlyList<BondFaceValueRequest> requests,
        CancellationToken cancellationToken);

    /// <summary>Возвращает валюты, доступные для торговли облигациями, для списка выбора на странице кеша.</summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Уникальные коды валют.</returns>
    Task<IReadOnlyList<string>> GetTradingCurrenciesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetFaceUnitsAsync(CancellationToken cancellationToken);
}
