using System.Text.Json.Serialization;

namespace Popo.HttpClients.Contracts.Iss;

public sealed class MoexDailyCandle
{
    [JsonPropertyName("begin")]
    public DateOnly? Begin { get; set; }

    [JsonPropertyName("close")]
    public decimal? Close { get; set; }
}
