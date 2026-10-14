using System.Text.Json.Serialization;

namespace Popo.HttpClients.Contracts.Iss;

public sealed class MoexIndexHistory
{
    [JsonPropertyName("TRADEDATE")]
    public DateOnly? TradeDate { get; set; }

    [JsonPropertyName("SECID")]
    public string? SecId { get; set; }

    [JsonPropertyName("CLOSE")]
    public decimal? Close { get; set; }
}
