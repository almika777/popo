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

    /// <summary>Читает уникальные валюты номинала из сохранённых данных облигаций MOEX.</summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Коды валют номинала, указанные в данных облигаций.</returns>
    Task<IReadOnlyList<string>> GetFaceUnitsAsync(CancellationToken cancellationToken);

    /// <summary>Читает сохранённые характеристики облигаций и торговых режимов для расчёта рекомендаций.</summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Облигации с данными торгового режима и эмитента, если они доступны.</returns>
    Task<IReadOnlyList<BondSecurityDto>> GetSecuritiesAsync(CancellationToken cancellationToken);
}
