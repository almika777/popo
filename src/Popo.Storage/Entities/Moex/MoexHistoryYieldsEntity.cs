using System.Text.Json.Serialization;

namespace Popo.Storage.Entities.Moex;

public class MoexHistoryYieldsEntity
{
    /// <summary>Идентификатор финансового инструмента</summary>
    public string SecId { get; set; } = null!;

    /// <summary>Идентификатор режима торгов</summary>
    public string BoardId { get; set; } = null!;

    /// <summary>Дата торгов</summary>
    public DateOnly TradeDate { get; set; }
    
    /// <summary>Количество сделок за день</summary>
    public double? NumTrades { get; set; }

    /// <summary>Объем торгов в валюте расчетов</summary>
    public double? Value { get; set; }

    /// <summary>Минимальная цена сделки (% от номинала)</summary>
    public double? Low { get; set; }

    /// <summary>Максимальная цена сделки (% от номинала)</summary>
    public double? High { get; set; }

    /// <summary>Цена последней сделки (%)</summary>
    public double? Close { get; set; }

    /// <summary>Цена закрытия (% от номинала)</summary>
    public double? LegalClosePrice { get; set; }

    /// <summary>Накопленный купонный доход (НКД)</summary>
    public double? AccInt { get; set; }

    /// <summary>Средневзвешенная цена (% от номинала)</summary>
    public double? WapPrice { get; set; }

    /// <summary>Доходность по последней сделке</summary>
    public double? YieldClose { get; set; }

    /// <summary>Цена открытия (% от номинала)</summary>
    public double? Open { get; set; }

    /// <summary>Объем сделок (количество бумаг)</summary>
    public double? Volume { get; set; }

    /// <summary>Рыночная цена (2), % от номинала</summary>
    public double? MarketPrice2 { get; set; }

    /// <summary>Рыночная цена (3), % от номинала</summary>
    public double? MarketPrice3 { get; set; }

    /// <summary>Объем сделок для расчета рыночной цены (2)</summary>
    public double? Mp2ValTrd { get; set; }

    /// <summary>Объем сделок для расчета рыночной цены (3)</summary>
    public double? MarketPrice3TradesValue { get; set; }

    /// <summary>Дата погашения</summary>
    public DateOnly? MatDate { get; set; }

    /// <summary>Дюрация (дни)</summary>
    public double? Duration { get; set; }

    /// <summary>Доходность по средневзвешенной цене</summary>
    public double? YieldAtWap { get; set; }

    /// <summary>Вмененная плавающая ставка</summary>
    public double? IricpiClose { get; set; }

    /// <summary>Вмененная инфляция (BEI)</summary>
    public double? BeiClose { get; set; }

    /// <summary>Ставка купона (%)</summary>
    public double CouponPercent { get; set; }

    /// <summary>Сумма купона</summary>
    public double CouponValue { get; set; }

    /// <summary>Дата, к которой рассчитывается доходность</summary>
    public DateOnly? BuybackDate { get; set; }

    /// <summary>Дата последней сделки</summary>
    public DateOnly? LastTradeDate { get; set; }

    /// <summary>Номинал</summary>
    public double FaceValue { get; set; }

    /// <summary>Валюта расчетов</summary>
    public string? CurrencyId { get; set; }

    /// <summary>Вмененная ключевая ставка ЦБ</summary>
    public double? CbrClose { get; set; }

    /// <summary>Доходность к оферте</summary>
    public double? YieldToOffer { get; set; }

    /// <summary>Доходность для последнего купона</summary>
    public double? YieldLastCoupon { get; set; }

    /// <summary>Дата оферты</summary>
    public DateOnly? OfferDate { get; set; }

    /// <summary>Валюта номинала</summary>
    public string? FaceUnit { get; set; }

    /// <summary>Номер торговой сессии</summary>
    public int? TradingSession { get; set; }

    /// <summary>Дата колл-опциона</summary>
    public DateOnly? CallOptionDate { get; set; }

    /// <summary>Доходность к колл-опциону</summary>
    public double? CallOptionYield { get; set; }

    /// <summary>Дюрация к колл-опциону</summary>
    public double? CallOptionDuration { get; set; }

    /// <summary>Дата пут-опциона</summary>
    public DateOnly? PutOptionDate { get; set; }

    /// <summary>Дата расчета доходности от эмитента</summary>
    public DateOnly? DateYieldFromIssuer { get; set; }

    /// <summary>Торговый день</summary>
    public DateOnly? TradeSessionDate { get; set; }

    /// <summary>Z-спред по цене последней сделки</summary>
    public double? ZSpread { get; set; }

    /// <summary>Z-спред по средневзвешенной цене</summary>
    public double? ZSpreadAtWapPrice { get; set; }

    /// <summary>Вид облигации</summary>
    public string? BondType { get; set; }

    /// <summary>Подвид облигации</summary>
    public string? BondSubType { get; set; }

    /// <summary>Краткое наименование</summary>
    public string? ShortName { get; set; }
}
