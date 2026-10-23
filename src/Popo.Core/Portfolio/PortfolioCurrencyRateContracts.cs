namespace Popo.Core.Portfolio;

/// <summary>Сохранённый курс одной валюты к рублю на дату.</summary>
/// <param name="CurrencyId">Код валюты.</param>
/// <param name="RateDate">Дата курса.</param>
/// <param name="RateToRub">Количество рублей за единицу валюты.</param>
public sealed record PortfolioCurrencyRateRecord(
    string CurrencyId,
    DateOnly RateDate,
    double RateToRub);
