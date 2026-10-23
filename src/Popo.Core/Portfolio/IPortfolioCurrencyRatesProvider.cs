namespace Popo.Core.Portfolio;

/// <summary>Читает сохранённые курсы валют для расчётов портфеля.</summary>
public interface IPortfolioCurrencyRatesProvider
{
    /// <summary>Возвращает последний курс каждой запрошенной валюты не позже указанной даты.</summary>
    /// <param name="currencyIds">Коды валют для выборки.</param>
    /// <param name="asOf">Последняя допустимая дата курса.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Найденные курсы; неизвестные валюты отсутствуют в результате.</returns>
    Task<IReadOnlyList<PortfolioCurrencyRateRecord>> GetLatestRatesAsync(
        IReadOnlyCollection<string> currencyIds,
        DateOnly asOf,
        CancellationToken cancellationToken);

    /// <summary>Возвращает валюты, для которых есть курс не позже указанной даты.</summary>
    /// <param name="asOf">Последняя допустимая дата курса.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Отсортированные коды доступных валют.</returns>
    Task<IReadOnlyList<string>> GetAvailableCurrencyIdsAsync(
        DateOnly asOf,
        CancellationToken cancellationToken);
}
