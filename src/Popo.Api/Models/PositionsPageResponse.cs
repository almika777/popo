namespace Popo.Api.Models;

public sealed record PositionsPageResponse(
    IReadOnlyList<PositionResponse> Positions,
    PositionTotalsResponse Totals);

public sealed record PositionTotalsResponse(
    double? MarketValueRub,
    double? InvestedAmountRub,
    double? UnrealizedPnlRub,
    double? UnrealizedPnlPercent);

public sealed record PositionResponse
{
    /// <summary>Код облигации на бирже.</summary>
    public required string SecId { get; init; }

    /// <summary>Код режима торгов.</summary>
    public required string BoardId { get; init; }

    /// <summary>Валюта торгов, в которой выражены цены и результаты позиции.</summary>
    public required string CurrencyId { get; init; }

    /// <summary>Краткое наименование облигации.</summary>
    public required string ShortName { get; init; }

    /// <summary>Тип облигации по данным MOEX.</summary>
    public string? BondType { get; init; }

    /// <summary>Количество облигаций в открытом остатке.</summary>
    public int Quantity { get; init; }

    /// <summary>Средняя чистая цена покупки одной облигации в открытом остатке, без НКД и комиссии.</summary>
    public double AvgBuyPrice { get; init; }

    /// <summary>Текущая чистая рыночная цена одной облигации в валюте торгов.</summary>
    public double MarketPrice { get; init; }

    /// <summary>Текущая рыночная стоимость открытого остатка без НКД: количество, умноженное на рыночную цену.</summary>
    public double TotalCost { get; init; }

    /// <summary>Переоценка тела открытой позиции: рыночная стоимость остатка минус его чистая себестоимость.</summary>
    public double UnrealizedPnl { get; init; }

    /// <summary>Приблизительный купонный доход открытого остатка за время владения в валюте торгов.</summary>
    public double CouponIncome { get; init; }

    /// <summary>Приблизительный результат открытой позиции: переоценка тела плюс оценка купонного дохода минус комиссия покупки на остаток.</summary>
    public double TotalPnl { get; init; }
}
