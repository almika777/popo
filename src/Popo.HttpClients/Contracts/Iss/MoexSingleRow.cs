using System.Text.Json.Serialization;

namespace Popo.HttpClients.Contracts.Iss;

public class MoexSingleRow
{
    [JsonPropertyName("NAME")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("VALUE")]
    public string? Value { get; set; }
    
}



