using System.Text.Json.Serialization;

namespace Popo.HttpClients.Contracts.Iss;

public class MoexMarketdataYields
{
    /// <summary>
    /// Код бумаги на МОЕХ
    /// </summary>
    [JsonPropertyName("SECID")]
    public string SecId { get; set; } = null!;
        
    /// <summary>
    /// Режим торгов
    /// </summary>
    [JsonPropertyName("BOARDID")]
    public string BoardId { get; set; } = null!;
    
    /// <summary>
    /// Цена по которой была рассчитана доходность
    /// </summary>
    [JsonPropertyName("PRICE")]
    public double Price { get; set; }

    /// <summary>
    /// Дата, к которой рассчитывается доходность
    /// </summary>
    [JsonPropertyName("YIELDDATE")]
    [JsonConverter(typeof(MoexDateOnlyDateTimeOffsetJsonConverter))]
    public DateTimeOffset? YieldDate { get; set; }       

    /// <summary>
    /// Тип даты, к которой рассчитывается доходность параметра (offer, maturity, MBS)
    /// </summary>
    [JsonPropertyName("YIELDDATETYPE")]
    public string? YieldDateType { get; set; }    
        
    /// <summary>
    /// Эффективная доходность
    /// </summary>
    [JsonPropertyName("EFFECTIVEYIELD")]
    public double? EffectiveYield { get; set; }

    /// <summary>
    /// Дюрация
    /// </summary>
    [JsonPropertyName("DURATION")]
    public int? Duration { get; set; }

    /// <summary>
    /// Z-спред, базисные пункты
    /// </summary>
    [JsonPropertyName("ZSPREADBP")]
    public double? ZSpread { get; set; }

    /// <summary>
    /// G-спред, базисные пункты
    /// </summary>
    [JsonPropertyName("GSPREADBP")]
    public double? GSpread { get; set; }

    /// <summary>
    /// Средневзвешенная цена
    /// </summary>
    [JsonPropertyName("WAPRICE")]
    public double WaPrice { get; set; }

    /// <summary>
    /// Эффективная доходность по средневзвешенной цене
    /// </summary>
    [JsonPropertyName("EFFECTIVEYIELDWAPRICE")]
    public double? EffectiveYieldWaPrice { get; set; }

    /// <summary>
    /// Дюрация по средневзвешенной цене, дней
    /// </summary>
    [JsonPropertyName("DURATIONWAPRICE")]
    public double? DurationWaPrice { get; set; }

    /// <summary>
    /// Момент совершения сделки
    /// </summary>
    [JsonPropertyName("TRADEMOMENT")]
    public DateTimeOffset? TradeMoment { get; set; }
    
    /// <summary>
    /// Порядковый номер записи
    /// </summary>
    [JsonPropertyName("SEQNUM")]
    public long SeqNum { get; set; }    
}



