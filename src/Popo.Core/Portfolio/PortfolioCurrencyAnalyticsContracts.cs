namespace Popo.Core.Portfolio;

/// <summary>Тип актива, учитываемого в валютном срезе портфеля.</summary>
public enum PortfolioCurrencyAnalyticsAssetKind
{
    /// <summary>Позиция в ценной бумаге.</summary>
    Position,

    /// <summary>Позиция в фонде денежного рынка.</summary>
    MoneyMarketFund,

    /// <summary>Денежный остаток в одной валюте.</summary>
    Cash
}

/// <summary>Оцениваемая позиция портфеля в валюте торгов.</summary>
/// <param name="AssetId">Стабильный идентификатор актива.</param>
/// <param name="Kind">Тип актива.</param>
/// <param name="Name">Название актива для отображения.</param>
/// <param name="TradingCurrencyId">Код валюты цены и расчётов.</param>
/// <param name="FaceUnit">Необязательный код валюты номинала.</param>
/// <param name="Quantity">Количество бумаг или паёв.</param>
/// <param name="MarketPrice">Текущая цена одной бумаги или пая в валюте торгов.</param>
/// <param name="TradingCurrencyRateToRub">Доступный курс валюты торгов в рублях за единицу.</param>
public sealed record PortfolioCurrencyAnalyticsAsset(
    string AssetId,
    PortfolioCurrencyAnalyticsAssetKind Kind,
    string Name,
    string TradingCurrencyId,
    string? FaceUnit,
    double Quantity,
    double? MarketPrice,
    double? TradingCurrencyRateToRub);

/// <summary>Денежный остаток в валюте портфеля.</summary>
/// <param name="CurrencyId">Код валюты остатка.</param>
/// <param name="Amount">Сумма остатка в исходной валюте.</param>
/// <param name="RateToRub">Доступный текущий курс валюты в рублях за единицу.</param>
public sealed record PortfolioCurrencyAnalyticsCashBalance(
    string CurrencyId,
    double Amount,
    double? RateToRub);

/// <summary>Все исходные и временные ручные значения одного расчёта.</summary>
/// <param name="Assets">Позиции и фонды денежного рынка.</param>
/// <param name="CashBalances">Введённые денежные остатки.</param>
/// <param name="ManualPricesByAssetId">Ручные цены одной бумаги по идентификатору актива.</param>
/// <param name="ManualRatesByCurrencyId">Ручные курсы в рублях за единицу валюты.</param>
public sealed record PortfolioCurrencyAnalyticsInput(
    IReadOnlyList<PortfolioCurrencyAnalyticsAsset> Assets,
    IReadOnlyList<PortfolioCurrencyAnalyticsCashBalance> CashBalances,
    IReadOnlyDictionary<string, double> ManualPricesByAssetId,
    IReadOnlyDictionary<string, double> ManualRatesByCurrencyId);

/// <summary>Оценённая доля портфеля в валюте номинала или остатка.</summary>
/// <param name="CurrencyId">Код валютной группы.</param>
/// <param name="ValueRub">Оценённая сумма группы в рублях.</param>
/// <param name="Share">Доля в общей оценённой сумме от 0 до 1.</param>
public sealed record PortfolioCurrencyAnalyticsSlice(
    string CurrencyId,
    double ValueRub,
    double Share);

/// <summary>Актив в валютной группе с его оценкой на текущем расчёте.</summary>
/// <param name="AssetId">Стабильный идентификатор актива.</param>
/// <param name="Kind">Тип актива.</param>
/// <param name="Name">Название актива для отображения.</param>
/// <param name="CurrencyId">Валюта группы: номинала позиции или остатка.</param>
/// <param name="TradingCurrencyId">Валюта цены и курса для оценки.</param>
/// <param name="ValueRub">Рублёвая стоимость либо null, если актив не оценён.</param>
/// <param name="MissingPrice">Признак отсутствующей цены одной бумаги.</param>
/// <param name="MissingRate">Признак отсутствующего курса валюты.</param>
public sealed record PortfolioCurrencyAnalyticsItem(
    string AssetId,
    PortfolioCurrencyAnalyticsAssetKind Kind,
    string Name,
    string CurrencyId,
    string TradingCurrencyId,
    double? ValueRub,
    bool MissingPrice,
    bool MissingRate);

/// <summary>Актив, для которого не хватает текущей оценки или курса.</summary>
/// <param name="AssetId">Стабильный идентификатор актива.</param>
/// <param name="Kind">Тип актива.</param>
/// <param name="Name">Название актива для отображения.</param>
/// <param name="CurrencyId">Код валютной группы.</param>
/// <param name="RateCurrencyId">Код валюты, курс которой требуется для рублёвой оценки.</param>
/// <param name="MissingPrice">Признак отсутствующей цены одной бумаги.</param>
/// <param name="MissingRate">Признак отсутствующего курса в рублях.</param>
public sealed record PortfolioCurrencyAnalyticsUnvaluedAsset(
    string AssetId,
    PortfolioCurrencyAnalyticsAssetKind Kind,
    string Name,
    string CurrencyId,
    string RateCurrencyId,
    bool MissingPrice,
    bool MissingRate);

/// <summary>Результат текущего среза портфеля по валютам.</summary>
/// <param name="TotalValueRub">Сумма оценённой части портфеля в рублях.</param>
/// <param name="Slices">Оценённые группы с долями.</param>
/// <param name="Items">Состав групп, включая неоценённые активы.</param>
/// <param name="UnvaluedAssets">Активы, которые не вошли в оценённую сумму.</param>
public sealed record PortfolioCurrencyAnalyticsResult(
    double TotalValueRub,
    IReadOnlyList<PortfolioCurrencyAnalyticsSlice> Slices,
    IReadOnlyList<PortfolioCurrencyAnalyticsItem> Items,
    IReadOnlyList<PortfolioCurrencyAnalyticsUnvaluedAsset> UnvaluedAssets);
