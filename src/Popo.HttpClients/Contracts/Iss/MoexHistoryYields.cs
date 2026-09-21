using System.Text.Json.Serialization;

namespace Popo.HttpClients.Contracts.Iss;

public class MoexHistoryYields
{
    /// <summary>Идентификатор режима торгов</summary>
    [JsonPropertyName("BOARDID")]
    public string? BoardId { get; set; }

    /// <summary>Дата торгов</summary>
    [JsonPropertyName("TRADEDATE")]
    public DateOnly? TradeDate { get; set; }

    /// <summary>Идентификатор финансового инструмента</summary>
    [JsonPropertyName("SECID")]
    public string? SecId { get; set; }

    /// <summary>Количество сделок за день</summary>
    [JsonPropertyName("NUMTRADES")]
    public double? NumTrades { get; set; }

    /// <summary>Объем торгов в валюте расчетов</summary>
    [JsonPropertyName("VALUE")]
    public double? Value { get; set; }

    /// <summary>Минимальная цена сделки (% от номинала)</summary>
    [JsonPropertyName("LOW")]
    public double? Low { get; set; }

    /// <summary>Максимальная цена сделки (% от номинала)</summary>
    [JsonPropertyName("HIGH")]
    public double? High { get; set; }

    /// <summary>Цена последней сделки (%)</summary>
    [JsonPropertyName("CLOSE")]
    public double? Close { get; set; }

    /// <summary>Цена закрытия (% от номинала)</summary>
    [JsonPropertyName("LEGALCLOSEPRICE")]
    public double? LegalClosePrice { get; set; }

    /// <summary>Накопленный купонный доход (НКД)</summary>
    [JsonPropertyName("ACCINT")]
    public double? AccInt { get; set; }

    /// <summary>Средневзвешенная цена (% от номинала)</summary>
    [JsonPropertyName("WAPRICE")]
    public double? WapPrice { get; set; }

    /// <summary>Доходность по последней сделке</summary>
    [JsonPropertyName("YIELDCLOSE")]
    public double? YieldClose { get; set; }

    /// <summary>Цена открытия (% от номинала)</summary>
    [JsonPropertyName("OPEN")]
    public double? Open { get; set; }

    /// <summary>Объем сделок (количество бумаг)</summary>
    [JsonPropertyName("VOLUME")]
    public double? Volume { get; set; }

    /// <summary>Рыночная цена (2), % от номинала</summary>
    [JsonPropertyName("MARKETPRICE2")]
    public double? MarketPrice2 { get; set; }

    /// <summary>Рыночная цена (3), % от номинала</summary>
    [JsonPropertyName("MARKETPRICE3")]
    public double? MarketPrice3 { get; set; }

    /// <summary>Объем сделок для расчета рыночной цены (2)</summary>
    [JsonPropertyName("MP2VALTRD")]
    public double? Mp2ValTrd { get; set; }

    /// <summary>Объем сделок для расчета рыночной цены (3)</summary>
    [JsonPropertyName("MARKETPRICE3TRADESVALUE")]
    public double? MarketPrice3TradesValue { get; set; }

    /// <summary>Дата погашения</summary>
    [JsonPropertyName("MATDATE")]
    public DateOnly? MatDate { get; set; }

    /// <summary>Дюрация (дни)</summary>
    [JsonPropertyName("DURATION")]
    public double? Duration { get; set; }

    /// <summary>Доходность по средневзвешенной цене</summary>
    [JsonPropertyName("YIELDATWAP")]
    public double? YieldAtWap { get; set; }

    /// <summary>Вмененная плавающая ставка</summary>
    [JsonPropertyName("IRICPICLOSE")]
    public double? IricpiClose { get; set; }

    /// <summary>Вмененная инфляция (BEI)</summary>
    [JsonPropertyName("BEICLOSE")]
    public double? BeiClose { get; set; }

    /// <summary>Ставка купона (%)</summary>
    [JsonPropertyName("COUPONPERCENT")]
    public double? CouponPercent { get; set; }

    /// <summary>Сумма купона</summary>
    [JsonPropertyName("COUPONVALUE")]
    public double? CouponValue { get; set; }

    /// <summary>Дата, к которой рассчитывается доходность</summary>
    [JsonPropertyName("BUYBACKDATE")]
    public DateOnly? BuybackDate { get; set; }

    /// <summary>Дата последней сделки</summary>
    [JsonPropertyName("LASTTRADEDATE")]
    public DateOnly? LastTradeDate { get; set; }

    /// <summary>Непогашенный долг</summary>
    [JsonPropertyName("FACEVALUE")]
    public double? FaceValue { get; set; }

    /// <summary>Валюта расчетов</summary>
    [JsonPropertyName("CURRENCYID")]
    public string? CurrencyId { get; set; }

    /// <summary>Вмененная ключевая ставка ЦБ</summary>
    [JsonPropertyName("CBRCLOSE")]
    public double? CbrClose { get; set; }

    /// <summary>Доходность к оферте</summary>
    [JsonPropertyName("YIELDTOOFFER")]
    public double? YieldToOffer { get; set; }

    /// <summary>Доходность для последнего купона</summary>
    [JsonPropertyName("YIELDLASTCOUPON")]
    public double? YieldLastCoupon { get; set; }

    /// <summary>Дата оферты</summary>
    [JsonPropertyName("OFFERDATE")]
    public DateOnly? OfferDate { get; set; }

    /// <summary>Валюта номинала</summary>
    [JsonPropertyName("FACEUNIT")]
    public string? FaceUnit { get; set; }

    /// <summary>Номер торговой сессии</summary>
    [JsonPropertyName("TRADINGSESSION")]
    public int? TradingSession { get; set; }

    /// <summary>Дата колл-опциона</summary>
    [JsonPropertyName("CALLOPTIONDATE")]
    public DateOnly? CallOptionDate { get; set; }

    /// <summary>Доходность к колл-опциону</summary>
    [JsonPropertyName("CALLOPTIONYIELD")]
    public double? CallOptionYield { get; set; }

    /// <summary>Дюрация к колл-опциону</summary>
    [JsonPropertyName("CALLOPTIONDURATION")]
    public double? CallOptionDuration { get; set; }

    /// <summary>Дата пут-опциона</summary>
    [JsonPropertyName("PUTOPTIONDATE")]
    public DateOnly? PutOptionDate { get; set; }

    /// <summary>Дата расчета доходности от эмитента</summary>
    [JsonPropertyName("DATEYIELDFROMISSUER")]
    public DateOnly? DateYieldFromIssuer { get; set; }

    /// <summary>Торговый день</summary>
    [JsonPropertyName("TRADE_SESSION_DATE")]
    public DateOnly? TradeSessionDate { get; set; }

    /// <summary>Z-спред по цене последней сделки</summary>
    [JsonPropertyName("ZSPREAD")]
    public double? ZSpread { get; set; }

    /// <summary>Z-спред по средневзвешенной цене</summary>
    [JsonPropertyName("ZSPREADATWAPRICE")]
    public double? ZSpreadAtWapPrice { get; set; }

    /// <summary>Вид облигации</summary>
    [JsonPropertyName("BONDTYPE")]
    public string? BondType { get; set; }

    /// <summary>Подвид облигации</summary>
    [JsonPropertyName("BONDSUBTYPE")]
    public string? BondSubType { get; set; }

    /// <summary>Краткое наименование</summary>
    [JsonPropertyName("SHORTNAME")]
    public string? ShortName { get; set; }
}


