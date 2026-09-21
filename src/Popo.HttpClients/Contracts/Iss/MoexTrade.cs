using System.Text.Json.Serialization;

namespace Popo.HttpClients.Contracts.Iss;

public class MoexTrade
{
    [JsonPropertyName("TRADETIME")]
    public string? TradeTime { get; set; }

    [JsonPropertyName("PRICE")]
    public double? Price { get; set; }

    [JsonPropertyName("QUANTITY")]
    public double? Quantity { get; set; }

    [JsonPropertyName("VALUE")]
    public double? Value { get; set; }

    [JsonPropertyName("YIELD")]
    public double? Yield { get; set; }

    [JsonPropertyName("BUYSELL")]
    public string? BuySell { get; set; }
}



