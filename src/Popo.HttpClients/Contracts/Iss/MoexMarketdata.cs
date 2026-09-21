using System.Text.Json.Serialization;

namespace Popo.HttpClients.Contracts.Iss;

public class MoexMarketdata
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
    /// Эффективная доходность по последней реальной сделке внутри дня.
    /// </summary>
    [JsonPropertyName("YIELD")]
    public double Yield { get; set; }

    /// <summary>
    /// Спрос
    /// </summary>
    [JsonPropertyName("BID")]
    public double? Bid { get; set; }

    /// <summary>
    /// Цена последней сделки, %
    /// </summary>
    [JsonPropertyName("LAST")]
    public double? Last { get; set; }

    /// <summary>
    /// Текущая цена
    /// </summary>
    [JsonPropertyName("LCURRENTPRICE")]
    public double? LCurrentPrice { get; set; }

    /// <summary>
    /// Рыночная цена 2
    /// </summary>
    [JsonPropertyName("MARKETPRICE2")]
    public double? MarketPrice2 { get; set; }

    /// <summary>
    /// Рыночная цена
    /// </summary>
    [JsonPropertyName("MARKETPRICE")]
    public double? MarketPrice { get; set; }

    /// <summary>
    /// Цена закрытия
    /// </summary>
    [JsonPropertyName("LCLOSEPRICE")]
    public double? LClosePrice { get; set; }

    /// <summary>
    /// Цена закрытия
    /// </summary>
    [JsonPropertyName("CLOSEPRICE")]
    public double? ClosePrice { get; set; }
    
    /// <summary>
    /// Дюрация, дни
    /// </summary>
    [JsonPropertyName("DURATION")]
    public int Duration { get; set; }

    /// <summary>
    /// Время загрузки данных системой
    /// </summary>
    [JsonPropertyName("SYSTIME")]
    [JsonConverter(typeof(MoexDateTimeJsonConverter))]
    public DateTime? SysTime { get; set; }

    
    /// <summary>
    /// Текущая цена
    /// </summary>
    public double? CurrentPrice => LCurrentPrice
                                   ?? MarketPrice2
                                   ?? MarketPrice
                                   ?? LClosePrice
                                   ?? ClosePrice
                                   ?? Last;

}
