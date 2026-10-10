using System.Text.Json.Serialization;

namespace Popo.HttpClients.Contracts.Iss;

internal sealed class MoexShareDailyHistory
{
    [JsonPropertyName("TRADEDATE")]
    public DateOnly? TradeDate { get; set; }

    [JsonPropertyName("SECID")]
    public string? SecId { get; set; }

    [JsonPropertyName("BOARDID")]
    public string? BoardId { get; set; }

    [JsonPropertyName("LEGALCLOSEPRICE")]
    public decimal? LegalClosePrice { get; set; }
}
