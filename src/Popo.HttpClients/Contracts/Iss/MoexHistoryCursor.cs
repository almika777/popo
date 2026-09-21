using System.Text.Json.Serialization;

namespace Popo.HttpClients.Contracts.Iss;

public class MoexHistoryCursor
{
    [JsonPropertyName("INDEX")]
    public int Index { get; set; }

    [JsonPropertyName("TOTAL")]
    public int Total { get; set; }

    [JsonPropertyName("PAGESIZE")]
    public int PageSize { get; set; }
}



