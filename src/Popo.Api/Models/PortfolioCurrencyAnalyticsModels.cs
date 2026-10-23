using Popo.Core.Portfolio;

namespace Popo.Api.Models;

/// <summary>Введённый денежный остаток для временного расчёта аналитики.</summary>
/// <param name="CurrencyId">Код валюты остатка.</param>
/// <param name="Amount">Сумма в исходной валюте.</param>
public sealed record PortfolioCurrencyAnalyticsCashBalanceRequest(string CurrencyId, double Amount);

/// <summary>Ручная цена одной бумаги или пая в валюте торгов.</summary>
/// <param name="AssetId">Стабильный идентификатор актива.</param>
/// <param name="Price">Цена одной бумаги или пая.</param>
public sealed record PortfolioCurrencyAnalyticsManualPriceRequest(string AssetId, double Price);

/// <summary>Ручной курс валюты к рублю.</summary>
/// <param name="CurrencyId">Код валюты.</param>
/// <param name="RateToRub">Количество рублей за единицу валюты.</param>
public sealed record PortfolioCurrencyAnalyticsManualRateRequest(string CurrencyId, double RateToRub);

/// <summary>Временные данные для одного расчёта состава портфеля по валютам.</summary>
/// <param name="CashBalances">Денежные остатки, не сохраняемые на сервере.</param>
/// <param name="ManualPrices">Ручные цены активов без текущей котировки.</param>
/// <param name="ManualRates">Ручные курсы валют без актуального курса.</param>
public sealed record PortfolioCurrencyAnalyticsRequest(
    IReadOnlyList<PortfolioCurrencyAnalyticsCashBalanceRequest> CashBalances,
    IReadOnlyList<PortfolioCurrencyAnalyticsManualPriceRequest> ManualPrices,
    IReadOnlyList<PortfolioCurrencyAnalyticsManualRateRequest> ManualRates);

/// <summary>Оценённая валютная группа портфеля.</summary>
/// <param name="CurrencyId">Код валютной группы.</param>
/// <param name="ValueRub">Сумма группы в рублях.</param>
/// <param name="Share">Доля группы в оценённой сумме от 0 до 1.</param>
public sealed record PortfolioCurrencyAnalyticsSliceResponse(
    string CurrencyId,
    double ValueRub,
    double Share);

/// <summary>Актив, который пока не удалось оценить полностью.</summary>
/// <param name="AssetId">Стабильный идентификатор актива.</param>
/// <param name="Kind">Тип актива.</param>
/// <param name="Name">Название актива.</param>
/// <param name="CurrencyId">Код валютной группы.</param>
/// <param name="MissingPrice">Не хватает цены одной бумаги или пая.</param>
/// <param name="MissingRate">Не хватает курса валюты к рублю.</param>
public sealed record PortfolioCurrencyAnalyticsUnvaluedAssetResponse(
    string AssetId,
    PortfolioCurrencyAnalyticsAssetKind Kind,
    string Name,
    string CurrencyId,
    bool MissingPrice,
    bool MissingRate);

/// <summary>Срез портфеля по валютным группам и доступные валюты для денежных остатков.</summary>
/// <param name="AsOf">Дата актуальности текущих данных.</param>
/// <param name="TotalValueRub">Оценённая сумма портфеля в рублях.</param>
/// <param name="AvailableCurrencies">Коды валют, доступные для ввода денежных остатков.</param>
/// <param name="Slices">Оценённые валютные группы и их доли.</param>
/// <param name="UnvaluedAssets">Активы, исключённые из оценённой суммы.</param>
public sealed record PortfolioCurrencyAnalyticsResponse(
    DateOnly AsOf,
    double TotalValueRub,
    IReadOnlyList<string> AvailableCurrencies,
    IReadOnlyList<PortfolioCurrencyAnalyticsSliceResponse> Slices,
    IReadOnlyList<PortfolioCurrencyAnalyticsUnvaluedAssetResponse> UnvaluedAssets);
